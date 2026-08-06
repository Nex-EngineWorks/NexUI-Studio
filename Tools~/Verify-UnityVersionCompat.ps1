<#
.SYNOPSIS
    Compiles the NexUI packages against a specific Unity editor's assemblies.

.DESCRIPTION
    NexUI claims Unity 2022.3 LTS and Unity 6 in package.json. Nothing in the normal
    workflow proves the 2022.3 half of that claim: the project opens in Unity 6, so an
    API that only exists in Unity 6 compiles fine here and fails at a user's first import.

    This script closes that gap without a second Unity project. It runs the target
    editor's own Roslyn against the target editor's own assemblies, which is enough to
    catch the failure mode that actually happens - a type or overload that does not
    exist on the older editor.

    What it does NOT check:
      * asmdef boundaries - and this one has already bitten. Everything compiles as one
        assembly, so a type used without its assembly being referenced passes here and
        fails in Unity. Treat a pass as "no missing API on that editor", never as "this
        builds". For the installed editor, `Unity -batchmode -runTests` is strictly
        better and is the real gate; this script's unique value is the editor version
        you have no project for.
      * The [UxmlElement] source generator. It is not run, so the generated UXML
        serialization path is unverified either way.
      * Anything at runtime. A clean compile is not a passing test.

.PARAMETER EditorRoot
    Unity editor install, e.g. D:\unityEditor\2022.3.62f3. Defaults to the oldest
    editor found next to the one this project uses.

.EXAMPLE
    ./Tools/Verify-UnityVersionCompat.ps1 -EditorRoot D:\unityEditor\2022.3.62f3
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EditorRoot
)

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

$managed = Join-Path $EditorRoot 'Editor/Data/Managed'
$csc = Join-Path $EditorRoot 'Editor/Data/DotNetSdkRoslyn/csc.dll'
foreach ($required in @($managed, $csc)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Not a usable Unity install - missing '$required'."
    }
}

# 2023.2 is where [UxmlElement] and its source generator arrived. Below that the
# packages take the hand-written UXML path in the *.Legacy.cs files. Parsed rather than
# pattern-matched: getting this backwards would compile the wrong half of every #if and
# still report success, which is worse than not running the check at all.
$editorVersion = Split-Path -Leaf $EditorRoot
if ($editorVersion -notmatch '^(?<major>\d+)\.(?<minor>\d+)') {
    throw "Cannot read a Unity version from folder name '$editorVersion'."
}
$major = [int]$Matches['major']
$minor = [int]$Matches['minor']
$isModern = ($major -gt 2023) -or ($major -eq 2023 -and $minor -ge 2)

$work = Join-Path ([System.IO.Path]::GetTempPath()) "nexui-compat-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $work -Force | Out-Null

function New-ReferenceFile([string[]]$Paths, [string]$File) {
    $lines = foreach ($path in $Paths) {
        if (Test-Path -LiteralPath $path) { "-r:`"$((Resolve-Path -LiteralPath $path).Path)`"" }
    }
    Set-Content -LiteralPath $File -Value $lines -Encoding utf8
}

$netRef = Get-ChildItem 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name | Select-Object -Last 1
if (-not $netRef) { throw 'No Microsoft.NETCore.App.Ref found. Install a .NET SDK.' }
$netRefDir = (Get-ChildItem (Join-Path $netRef.FullName 'ref') -Directory | Sort-Object Name | Select-Object -Last 1).FullName

$bcl = @(
    'netstandard', 'System.Runtime', 'System.Collections', 'System.Runtime.Extensions',
    'System.Linq', 'System.Linq.Expressions', 'System.Threading', 'System.Text.RegularExpressions',
    'System.ObjectModel', 'System.Net.Primitives', 'System.Security.Cryptography',
    'System.Text.Encoding.Extensions', 'System.Xml.ReaderWriter', 'System.ComponentModel.Primitives',
    'System.ComponentModel.TypeConverter', 'System.IO.FileSystem',
    # NUnit ships a net40 build whose attributes are typed against mscorlib; without the facade
    # every [Test] reports CS0012.
    'mscorlib', 'System', 'System.Core'
) | ForEach-Object { Join-Path $netRefDir "$_.dll" }

$unityModules = Get-ChildItem (Join-Path $managed 'UnityEngine') -Filter '*.dll' | ForEach-Object { $_.FullName }
# ExCSS.Unity is deliberately not here. On 2022.3 it defines its own Tuple, which collides with
# System.Runtime's for anything that also references the BCL facades - a clash Unity never sees
# because no NexUI asmdef references ExCSS. Add per-compile if a target genuinely needs it.
$extras = @('Newtonsoft.Json.dll') | ForEach-Object { Join-Path $managed $_ }

# Package assemblies NexUI references but does not own. Taken from this project's last
# Unity compile: they are version-agnostic enough to reference, and the point of the run
# is NexUI's own sources, not theirs.
$scriptAssemblies = Join-Path $projectRoot 'Library/ScriptAssemblies'
$thirdParty = @('UniTask', 'UnityEngine.UI', 'Unity.TextMeshPro', 'Unity.TextMeshPro.Editor', 'UnityEditor.UI') |
    ForEach-Object { Join-Path $scriptAssemblies "$_.dll" }

$defines = @()
if ($isModern) { $defines += 'UNITY_2023_2_OR_NEWER' }

# Stands in for the version define on emiteat.NexUI.Vector.asmdef. Unity.VectorGraphics is a
# built-in module on Unity 6 and absent on 2022.3, so presence of the module assembly is the same
# question the asmdef asks. Deciding it here rather than hardcoding a version is what makes a
# 2022.3 project that installed com.unity.vectorgraphics compile the same code Unity 6 does.
$hasVectorGraphics = Test-Path -LiteralPath (Join-Path $managed 'UnityEngine/UnityEngine.VectorGraphicsModule.dll')
if ($hasVectorGraphics) { $defines += 'NEXUI_VECTOR_GRAPHICS' }

$define = if ($defines.Count -gt 0) { "-define:$($defines -join ';')" } else { '' }
$failures = 0

function Invoke-Compile([string]$Name, [string[]]$Roots, [string[]]$ExtraRefs, [string]$Output) {
    $sources = foreach ($root in $Roots) {
        if (Test-Path -LiteralPath $root) {
            Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs' | ForEach-Object { "`"$($_.FullName)`"" }
        }
    }
    if (-not $sources) { Write-Host "  $Name : no sources, skipped"; return $true }

    $sourceFile = Join-Path $work "$Name.sources.rsp"
    $refFile = Join-Path $work "$Name.refs.rsp"
    Set-Content -LiteralPath $sourceFile -Value $sources -Encoding utf8
    New-ReferenceFile ($bcl + $unityModules + $extras + $thirdParty + $ExtraRefs) $refFile

    $log = Join-Path $work "$Name.log"
    $arguments = @($csc, '-target:library', "-out:$Output", '-nostdlib+', '-langversion:9.0')
    if ($define) { $arguments += $define }
    if ($script:extraDefines) { $arguments += "-define:$($script:extraDefines)" }
    $arguments += @("@$refFile", "@$sourceFile")

    & dotnet @arguments *> $log
    $errors = @(Select-String -LiteralPath $log -Pattern 'error CS' -ErrorAction SilentlyContinue)

    if ($errors.Count -eq 0) {
        Write-Host "  $Name : OK ($($sources.Count) files)" -ForegroundColor Green
        return $true
    }

    Write-Host "  $Name : $($errors.Count) error(s)" -ForegroundColor Red
    $errors | Select-Object -First 15 | ForEach-Object { Write-Host "    $($_.Line.Trim())" -ForegroundColor Red }
    return $false
}

Write-Host "NexUI compatibility check against $editorVersion" -ForegroundColor Cyan

$runtimeDll = Join-Path $work 'nexui.runtime.dll'
$core = Join-Path $projectRoot 'Packages/com.nexengineworks.nexui'
$studio = Join-Path $projectRoot 'Packages/com.nexengineworks.nexui.studio'

# Optional integrations are excluded: their defineConstraints keep them out of a default
# project, and their third-party dependencies are not present here.
if (-not (Invoke-Compile 'runtime' @(
        (Join-Path $core 'Runtime'),
        (Join-Path $core 'Integrations/UGUI'),
        (Join-Path $core 'Integrations/UIToolkit')
    ) @() $runtimeDll)) { $failures++ }

# Optional integrations are skipped above because their defineConstraints keep them out of a
# default project. DOTween is the exception worth checking: it ships as a plugin DLL that is
# present in this repo, and the integration went uncompiled for a long time precisely because
# nothing ever built it.
$dotween = Get-ChildItem (Join-Path $projectRoot 'Assets') -Recurse -Filter 'DOTween.dll' `
    -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch 'Editor' } | Select-Object -First 1

if ($dotween -and (Test-Path -LiteralPath $runtimeDll)) {
    $script:extraDefines = 'DOTWEEN'
    if (-not (Invoke-Compile 'integration-dotween' @(
            (Join-Path $core 'Integrations/DOTween')
        ) @($runtimeDll, $dotween.FullName) (Join-Path $work 'nexui.dotween.dll'))) { $failures++ }
    $script:extraDefines = $null
}
elseif (-not $dotween) {
    Write-Host '  integration-dotween : skipped, DOTween.dll not found under Assets/' -ForegroundColor Yellow
}

$studioDll = Join-Path $work 'nexui.studio.dll'
if (Test-Path -LiteralPath $runtimeDll) {
    if (-not (Invoke-Compile 'studio' @(
            (Join-Path $studio 'Runtime'),
            (Join-Path $studio 'Editor')
        ) @($runtimeDll) $studioDll)) { $failures++ }
}
else {
    Write-Host '  studio : skipped, runtime did not build' -ForegroundColor Yellow
    $failures++
}

# Tests compile against the same target so a test referencing an API that only exists on the
# newer editor is caught here rather than at the end of a batchmode run.
$nunit = Get-ChildItem (Join-Path $projectRoot 'Library/PackageCache') -Recurse -Filter 'nunit.framework.dll' `
    -ErrorAction SilentlyContinue | Select-Object -First 1
$testRefs = @($runtimeDll, $studioDll,
    (Join-Path $scriptAssemblies 'UnityEngine.TestRunner.dll'),
    (Join-Path $scriptAssemblies 'UnityEditor.TestRunner.dll'))
if ($nunit) { $testRefs += $nunit.FullName }

if ($nunit -and (Test-Path -LiteralPath $studioDll)) {
    # One invocation per test assembly, not one for all of them. EditMode and PlayMode each
    # define their own Fakes in the same namespace - legal across assemblies, a redefinition
    # inside one - so merging them would report 900 collisions that Unity never sees.
    $testRoots = @{
        'tests-core-edit'   = Join-Path $core 'Tests/EditMode'
        'tests-core-play'   = Join-Path $core 'Tests/PlayMode'
        'tests-studio-edit' = Join-Path $studio 'Tests/EditMode'
        'tests-studio-play' = Join-Path $studio 'Tests/PlayMode'
    }
    $fixtures = Join-Path $studio 'Tests/Fixtures'

    foreach ($name in ($testRoots.Keys | Sort-Object)) {
        $roots = @($testRoots[$name])
        if ($name -like 'tests-studio-*' -and (Test-Path -LiteralPath $fixtures)) { $roots += $fixtures }
        if (-not (Invoke-Compile $name $roots $testRefs (Join-Path $work "$name.dll"))) { $failures++ }
    }
}
elseif (-not $nunit) {
    Write-Host '  tests : skipped, nunit.framework.dll not found in Library/PackageCache' -ForegroundColor Yellow
}
else {
    # Say which of the two reasons it was. Reporting the missing dependency when the real cause
    # was the failure above sends the reader off to fix the wrong thing.
    Write-Host '  tests : skipped, studio did not build' -ForegroundColor Yellow
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue

if ($failures -gt 0) {
    Write-Host "FAILED against $editorVersion." -ForegroundColor Red
    exit 1
}
Write-Host "NexUI compiles against $editorVersion." -ForegroundColor Green
