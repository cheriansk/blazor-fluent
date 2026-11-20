<#
.SYNOPSIS
    Finds all files tagged with [NEED-HUMAN-REVIEW] or "_needHumanReview": true.
.DESCRIPTION
    Scans src/, tests/, scripts/, and docs/ (excluding generated HTML in datadictionary)
    for files created or modified by AI agents that are still awaiting human review.
#>
param(
    [switch]$FailIfFound
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$searchRoots = @("src", "tests", "scripts", "docs") |
    ForEach-Object { Join-Path $repoRoot $_ } |
    Where-Object { Test-Path $_ }

$matchesList = [System.Collections.Generic.List[string]]::new()

foreach ($root in $searchRoots) {
    $files = Get-ChildItem -Path $root -Recurse -File -Include *.cs,*.razor,*.css,*.js,*.ts,*.md,*.json,*.ps1,*.py,*.yml |
        Where-Object {
            $_.FullName -notmatch '\\(bin|obj|node_modules)\\' -and
            $_.FullName -notmatch '\\datadictionary\\.*\.html$'
        }

    foreach ($file in $files) {
        $hit = Select-String -Path $file.FullName -Pattern '\[NEED-HUMAN-REVIEW\]|"_needHumanReview"\s*:\s*true' -Quiet
        if ($hit) {
            $relPath = [System.IO.Path]::GetRelativePath($repoRoot, $file.FullName)
            $matchesList.Add($relPath)
        }
    }
}

if ($matchesList.Count -eq 0) {
    Write-Host "No files pending AI review." -ForegroundColor Green
    exit 0
}

Write-Host "Files pending human review ($($matchesList.Count)):" -ForegroundColor Yellow
foreach ($item in $matchesList) {
    Write-Host "  - $item" -ForegroundColor Cyan
}

if ($FailIfFound) {
    exit 1
}
