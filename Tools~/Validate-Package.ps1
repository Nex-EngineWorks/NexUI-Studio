[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()
function Fail([string]$Message) { $errors.Add($Message) }

try {
    $manifest = Get-Content -LiteralPath (Join-Path $packageRoot 'package.json') -Raw | ConvertFrom-Json
    foreach ($field in @('name', 'displayName', 'version', 'unity', 'license', 'description')) {
        if ([string]::IsNullOrWhiteSpace([string]$manifest.$field)) { Fail "package.json is missing '$field'." }
    }
    if ([string]$manifest.version -notmatch '^\d+\.\d+\.\d+([+-][0-9A-Za-z.-]+)?$') { Fail "Invalid version '$($manifest.version)'." }
}
catch { Fail "Cannot parse package.json: $($_.Exception.Message)" }

foreach ($runtimeName in @('Runtime', 'Integrations')) {
    $runtimeRoot = Join-Path $packageRoot $runtimeName
    if (-not (Test-Path -LiteralPath $runtimeRoot)) { continue }
    foreach ($source in Get-ChildItem -LiteralPath $runtimeRoot -Recurse -File -Filter '*.cs') {
        foreach ($match in Select-String -LiteralPath $source.FullName -Pattern '^\s*using\s+UnityEditor(?:\.|\s*;)|\bUnityEditor\.') {
            Fail "Runtime UnityEditor reference: $($source.FullName):$($match.LineNumber)"
        }
    }
}

foreach ($rootName in @('Runtime', 'Editor', 'Integrations', 'Tests', 'Assets', 'Localization')) {
    $root = Join-Path $packageRoot $rootName
    if (-not (Test-Path -LiteralPath $root)) { continue }
    foreach ($entry in Get-ChildItem -LiteralPath $root -Recurse -Force) {
        if ($entry.Name.EndsWith('.meta') -or $entry.Name.StartsWith('.')) { continue }
        if (-not (Test-Path -LiteralPath ($entry.FullName + '.meta'))) { Fail "Missing meta file: $($entry.FullName).meta" }
    }
}

foreach ($markdown in Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Filter '*.md' |
    Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' }) {
    $content = Get-Content -LiteralPath $markdown.FullName -Raw
    foreach ($match in [regex]::Matches($content, '\[[^\]]*\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Trim().Trim('<', '>')
        if ($target -match '^(https?://|mailto:|#)' -or [string]::IsNullOrWhiteSpace($target)) { continue }
        $target = [uri]::UnescapeDataString(($target -split '#')[0])
        if (-not (Test-Path -LiteralPath (Join-Path $markdown.DirectoryName $target))) {
            Fail "Broken documentation link in $($markdown.FullName): $target"
        }
    }
}

foreach ($artifact in Get-ChildItem -LiteralPath $packageRoot -Recurse -File |
    Where-Object { $_.Name -match '\.(nexui\.tmp|orig|rej)$' -and $_.FullName -notmatch '[\\/]\.git[\\/]' }) {
    Fail "Unexpected generated/merge artifact: $($artifact.FullName)"
}
foreach ($textFile in Get-ChildItem -LiteralPath $packageRoot -Recurse -File |
    Where-Object { $_.Extension -in @('.cs', '.json', '.md', '.uxml', '.uss', '.asmdef') -and $_.FullName -notmatch '[\\/]\.git[\\/]' }) {
    if (Select-String -LiteralPath $textFile.FullName -Pattern '^(<<<<<<<|=======|>>>>>>>)' -Quiet) {
        Fail "Unresolved merge marker: $($textFile.FullName)"
    }
}

if ($errors.Count -gt 0) {
    foreach ($validationError in $errors) { Write-Error $validationError }
    exit 1
}
Write-Host 'NexUI Designer package validation passed.' -ForegroundColor Green
