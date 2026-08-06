[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Walks up looking for the folder that contains the packages, rather than assuming the script
# sits one level below it. The script lives inside a package (so it is version-controlled) but is
# also run from the Unity project root and from CI checkouts, and those put it at three different
# depths. Searching means moving the file never silently changes what it validates.
function Resolve-NexUIRoot([string]$Start) {
    $dir = $Start
    while ($dir) {
        if (Test-Path (Join-Path $dir 'Packages/com.nexengineworks.nexui')) { return $dir }
        # A bare package checkout has no Packages/ folder; its own parent stands in for one.
        if ((Split-Path -Leaf $dir) -eq 'Packages') { return (Split-Path -Parent $dir) }
        $dir = Split-Path -Parent $dir
    }
    return $null
}

$projectRoot = Resolve-NexUIRoot $PSScriptRoot
if (-not $projectRoot) {
    throw "Cannot find 'Packages/com.nexengineworks.nexui' above '$PSScriptRoot'."
}

$packageRoots = @(
    (Join-Path $projectRoot 'Packages/com.nexengineworks.nexui'),
    (Join-Path $projectRoot 'Packages/com.nexengineworks.nexui.studio')
)
$errors = [System.Collections.Generic.List[string]]::new()

function Add-ValidationError([string]$Message) {
    $errors.Add($Message)
}

# A renamed or moved package must fail as a reported validation error, not as an
# unhandled terminating error. The previous version threw out of the first
# Get-ChildItem, which made a stale path look like a broken script rather than a
# broken package layout - and took the whole CI test matrix down with it.
foreach ($packageRoot in $packageRoots) {
    if (-not (Test-Path -LiteralPath $packageRoot)) {
        Add-ValidationError "Package root does not exist: $packageRoot"
    }
}
$packageRoots = @($packageRoots | Where-Object { Test-Path -LiteralPath $_ })

foreach ($packageRoot in $packageRoots) {
    $manifestPath = Join-Path $packageRoot 'package.json'
    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        foreach ($field in @('name', 'displayName', 'version', 'unity', 'license', 'description')) {
            if ([string]::IsNullOrWhiteSpace([string]$manifest.$field)) {
                Add-ValidationError "$manifestPath is missing required field '$field'."
            }
        }
        if ([string]$manifest.version -notmatch '^\d+\.\d+\.\d+([+-][0-9A-Za-z.-]+)?$') {
            Add-ValidationError "$manifestPath has invalid semantic version '$($manifest.version)'."
        }
    }
    catch {
        Add-ValidationError "Cannot parse ${manifestPath}: $($_.Exception.Message)"
    }

    foreach ($runtimeRootName in @('Runtime', 'Integrations')) {
        $runtimeRoot = Join-Path $packageRoot $runtimeRootName
        if (-not (Test-Path -LiteralPath $runtimeRoot)) { continue }
        foreach ($source in Get-ChildItem -LiteralPath $runtimeRoot -Recurse -File -Filter '*.cs') {
            $matches = Select-String -LiteralPath $source.FullName -Pattern '^\s*using\s+UnityEditor(?:\.|\s*;)|\bUnityEditor\.'
            foreach ($match in $matches) {
                Add-ValidationError "Runtime UnityEditor reference: $($source.FullName):$($match.LineNumber)"
            }
        }
    }

    foreach ($importedRootName in @('Runtime', 'Editor', 'Integrations', 'Tests', 'Assets', 'Localization')) {
        $importedRoot = Join-Path $packageRoot $importedRootName
        if (-not (Test-Path -LiteralPath $importedRoot)) { continue }
        foreach ($entry in Get-ChildItem -LiteralPath $importedRoot -Recurse -Force) {
            if ($entry.Name.EndsWith('.meta')) { continue }
            if ($entry.Name.StartsWith('.')) { continue }
            $metaPath = $entry.FullName + '.meta'
            if (-not (Test-Path -LiteralPath $metaPath)) {
                Add-ValidationError "Missing Unity meta file: $metaPath"
            }
        }
    }
}

$markdownFiles = foreach ($packageRoot in $packageRoots) {
    Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Filter '*.md' |
        Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' }
}
foreach ($markdown in $markdownFiles) {
    $content = Get-Content -LiteralPath $markdown.FullName -Raw
    foreach ($match in [regex]::Matches($content, '!?(?<!\!)\[[^\]]*\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Trim().Trim('<', '>')
        if ($target -match '^(https?://|mailto:|#)' -or [string]::IsNullOrWhiteSpace($target)) { continue }
        $target = [uri]::UnescapeDataString(($target -split '#')[0])
        $resolved = Join-Path $markdown.DirectoryName $target
        if (-not (Test-Path -LiteralPath $resolved)) {
            Add-ValidationError "Broken documentation link in $($markdown.FullName): $target"
        }
    }
}

foreach ($packageRoot in $packageRoots) {
    foreach ($pattern in @('*.nexui.tmp', '*.orig', '*.rej')) {
        foreach ($artifact in Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Filter $pattern) {
            Add-ValidationError "Unexpected generated/merge artifact: $($artifact.FullName)"
        }
    }
    foreach ($textFile in Get-ChildItem -LiteralPath $packageRoot -Recurse -File |
        Where-Object { $_.Extension -in @('.cs', '.json', '.md', '.uxml', '.uss', '.asmdef') -and $_.FullName -notmatch '[\\/]\.git[\\/]' }) {
        if (Select-String -LiteralPath $textFile.FullName -Pattern '^(<<<<<<<|=======|>>>>>>>)' -Quiet) {
            Add-ValidationError "Unresolved merge marker: $($textFile.FullName)"
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Host "NexUI static validation failed with $($errors.Count) error(s):" -ForegroundColor Red
    foreach ($validationError in $errors) { Write-Host " - $validationError" -ForegroundColor Red }
    exit 1
}

Write-Host 'NexUI static validation passed.' -ForegroundColor Green
