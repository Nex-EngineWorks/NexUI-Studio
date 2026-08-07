<#
.SYNOPSIS
    Checks that every NexUI assembly declares the assemblies its source actually uses.

.DESCRIPTION
    Verify-UnityVersionCompat.ps1 compiles everything as one assembly, so a type used
    without its asmdef reference passes there and fails in Unity. That gap has already
    cost two batchmode runs - once for `using emiteat.NexUI.Settings` in the Studio,
    once for `using emiteat.NexUI.State` in a test - so it gets its own check.

    The check is deliberately narrow: for each NexUI assembly, take the namespaces its
    files import, map the NexUI ones to the assembly that owns them, and require that
    assembly to be referenced *directly*.

    Directly, not transitively. Unity does not propagate asmdef references: if A references
    B and B references Vector, A still cannot use Vector's types. An earlier version of this
    script accepted the transitive path and so passed a Studio Editor that used
    `emiteat.NexUI.Vector` through `Designer.Runtime` - which Unity then rejected, costing the
    batchmode run this script exists to protect.

    Non-NexUI namespaces are covered by a small fixed table rather than by resolving the
    whole package graph. Only the handful this repository actually imports needs to be
    known, and leaving them out cost a third batchmode run - a test using `TMPro` and
    `UnityEngine.UI` whose assembly declared neither.

    What it does NOT do:
      * Non-NexUI namespaces outside that table. Adding one is a line of script.
      * Fully qualified type use without a `using`. Those resolve for the same reason
        the reference exists, so they are not the failure mode this catches.

.EXAMPLE
    ./Tools/Verify-AsmdefReferences.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# See Validate-NexUI.ps1 for why this searches instead of assuming a fixed depth.
function Resolve-NexUIRoot([string]$Start) {
    $dir = $Start
    while ($dir) {
        if (Test-Path (Join-Path $dir 'Packages/com.nexengineworks.nexui')) { return $dir }
        if ((Split-Path -Leaf $dir) -eq 'Packages') { return (Split-Path -Parent $dir) }
        $dir = Split-Path -Parent $dir
    }
    return $null
}

$projectRoot = Resolve-NexUIRoot $PSScriptRoot
if (-not $projectRoot) {
    throw "Cannot find 'Packages/com.nexengineworks.nexui' above '$PSScriptRoot'."
}

$packagesRoot = Join-Path $projectRoot 'Packages'

# name -> @{ Dir; References }
$assemblies = @{}

# namespace -> every assembly that declares it.
# A set rather than one name: EditMode and PlayMode tests each declare their own
# emiteat.NexUI.Tests.Fakes, which is legal across assemblies. Recording a single owner
# would report the assembly that has its own copy as missing a reference to the other.
$namespaceOwners = @{}

foreach ($file in Get-ChildItem $packagesRoot -Recurse -Filter '*.asmdef' -ErrorAction SilentlyContinue) {
    if ($file.FullName -notmatch 'com\.nexengineworks') { continue }

    $json = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    if (-not $json.name) { continue }

    $assemblies[$json.name] = @{
        Dir         = $file.DirectoryName
        References  = @($json.references)

        # Kept separate because a precompiled DLL satisfies a using just as a reference does,
        # and test assemblies pull nunit in that way rather than by name.
        Precompiled = @($json.precompiledReferences)
    }
}

# An assembly owns every namespace declared in its own files.
foreach ($name in $assemblies.Keys) {
    foreach ($source in Get-ChildItem $assemblies[$name].Dir -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue) {
        foreach ($match in [regex]::Matches((Get-Content -LiteralPath $source.FullName -Raw),
                '(?m)^\s*namespace\s+([A-Za-z0-9_.]+)')) {
            $ns = $match.Groups[1].Value
            if ($ns -notlike 'emiteat.NexUI*') { continue }
            if (-not $namespaceOwners.ContainsKey($ns)) {
                $namespaceOwners[$ns] = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
            }
            [void]$namespaceOwners[$ns].Add($name)
        }
    }
}

# Namespaces this repository imports from outside NexUI, and the assembly each needs.
# Only what is actually used here - a full package index would be a lot of machinery to
# catch the same handful of mistakes.
$externalOwners = @{
    'TMPro'                        = 'Unity.TextMeshPro'
    'UnityEngine.UI'               = 'UnityEngine.UI'
    'UnityEngine.EventSystems'     = 'UnityEngine.UI'
    'NUnit.Framework'              = 'nunit.framework.dll'
    'UnityEngine.TestTools'        = 'UnityEngine.TestRunner'
    'UnityEditor.TestTools'        = 'UnityEditor.TestRunner'
}

# These arrive without being named in `references`, so requiring one would be wrong:
# nunit comes in through precompiledReferences, and the engine/editor assemblies are
# always available.
$alwaysAvailable = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@('nunit.framework.dll'), [System.StringComparer]::Ordinal)

$problems = [System.Collections.Generic.List[string]]::new()

foreach ($name in ($assemblies.Keys | Sort-Object)) {
    $declared = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]$assemblies[$name].References, [System.StringComparer]::Ordinal)

    $precompiled = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]$assemblies[$name].Precompiled, [System.StringComparer]::Ordinal)

    foreach ($source in Get-ChildItem $assemblies[$name].Dir -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue) {
        $text = Get-Content -LiteralPath $source.FullName -Raw
        $relative = $source.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')

        foreach ($match in [regex]::Matches($text, '(?m)^\s*using\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;')) {
            $ns = $match.Groups[1].Value

            if ($ns -like 'emiteat.NexUI*') {
                if (-not $namespaceOwners.ContainsKey($ns)) { continue }
                $owners = $namespaceOwners[$ns]

                # Satisfied if this assembly declares the namespace itself, or names an assembly
                # that does among its own references.
                if ($owners.Contains($name)) { continue }
                $satisfied = $false
                foreach ($owner in $owners) { if ($declared.Contains($owner)) { $satisfied = $true; break } }
                if ($satisfied) { continue }

                $problems.Add("$name : $relative uses '$ns' (in $($owners -join ', ')) without referencing it")
                continue
            }

            if (-not $externalOwners.ContainsKey($ns)) { continue }

            $required = $externalOwners[$ns]
            if ($declared.Contains($required) -or $precompiled.Contains($required)) { continue }
            if ($alwaysAvailable.Contains($required)) { continue }

            $problems.Add("$name : $relative uses '$ns' without referencing '$required'")
        }
    }
}

if ($problems.Count -gt 0) {
    Write-Host "Missing asmdef references ($($problems.Count)):" -ForegroundColor Red
    foreach ($problem in $problems) { Write-Host "  $problem" -ForegroundColor Red }
    exit 1
}

Write-Host "All NexUI asmdef references are declared ($($assemblies.Count) assemblies)." -ForegroundColor Green
