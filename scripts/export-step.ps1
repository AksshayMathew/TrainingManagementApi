<#
.SYNOPSIS
  Copies the files of ONE step (a tagged commit) of this repository into another folder, so you can
  review, build and commit them there - for example in a repository on your office laptop.

.DESCRIPTION
  Every step of this project is a Git commit tagged step-NN-<name> (list them with:  git tag -l "step-*").
  This script takes the files that step added or changed and copies them into -Target, and deletes the
  files that step removed. Nothing is committed for you: you build, test and commit in the target
  repository yourself, with your own identity and your own message.

.PARAMETER Step
  Step number, e.g. 4 for the tag step-04-employee-crud.

.PARAMETER Target
  Folder to copy into (created if missing) - normally the root of the other repository.

.PARAMETER Snapshot
  Copy the COMPLETE project as it is at that step instead of only the changes of that step.
  Use it once if you want to catch up to a later step in one go.

.PARAMETER ListOnly
  Only show what would be copied/removed; change nothing.

.EXAMPLE
  .\scripts\export-step.ps1 -Step 1 -Target C:\work\TrainingManagementApi
  # build + test in C:\work\TrainingManagementApi, commit, then:
  .\scripts\export-step.ps1 -Step 2 -Target C:\work\TrainingManagementApi

.EXAMPLE
  .\scripts\export-step.ps1 -Step 6 -Target C:\work\TrainingManagementApi -ListOnly
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateRange(0, 99)][int]$Step,
    [Parameter(Mandatory = $true)][string]$Target,
    [switch]$Snapshot,
    [switch]$ListOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Invoke-Git {
    param([string[]]$GitArguments)
    $output = & git -C $repoRoot @GitArguments
    if ($LASTEXITCODE -ne 0) { throw "git $($GitArguments -join ' ') failed." }
    return $output
}

# --- find the step ---------------------------------------------------------------------------------
$prefix = 'step-{0:D2}-' -f $Step
$tag = @(Invoke-Git @('tag', '-l', "$prefix*")) | Select-Object -First 1
if (-not $tag) {
    throw "No step tag starting with '$prefix'. See the available steps with:  git tag -l ""step-*"""
}

$subject = (Invoke-Git @('log', '-1', '--format=%s', $tag)) -join ' '
$body = @(Invoke-Git @('log', '-1', '--format=%b', $tag)) | Where-Object { $_ -notmatch '^\s*Co-Authored-By:' }

# --- work out which files to copy / remove -------------------------------------------------------------
$toCopy = New-Object System.Collections.Generic.List[string]
$toRemove = New-Object System.Collections.Generic.List[string]

if ($Snapshot) {
    foreach ($path in @(Invoke-Git @('ls-tree', '-r', '--name-only', $tag))) { if ($path) { $toCopy.Add($path) } }
}
else {
    foreach ($line in @(Invoke-Git @('diff-tree', '--no-commit-id', '--name-status', '-r', '--no-renames', '--root', $tag))) {
        if (-not $line) { continue }
        $parts = $line -split "`t", 2
        if ($parts[0] -eq 'D') { $toRemove.Add($parts[1]) } else { $toCopy.Add($parts[1]) }
    }
}

Write-Host ""
Write-Host "Step $Step  ($tag)" -ForegroundColor Cyan
Write-Host "  $subject"
$toCopy | ForEach-Object { Write-Host "  + $_" -ForegroundColor Green }
$toRemove | ForEach-Object { Write-Host "  - $_  (deleted in this step)" -ForegroundColor Yellow }

if ($ListOnly) {
    Write-Host ""
    Write-Host "ListOnly: nothing was changed." -ForegroundColor DarkGray
    return
}

# --- copy ----------------------------------------------------------------------------------------------
$targetFull = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Target).TrimEnd('\')

if ($targetFull -ieq $repoRoot -or $targetFull.StartsWith($repoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Target must be a folder OUTSIDE this repository ($repoRoot)."
}

New-Item -ItemType Directory -Force -Path $targetFull | Out-Null

# 'git archive | tar' copies bytes exactly (safe for the PNG screenshot), unlike PowerShell pipes.
function Expand-FromGit {
    param([string[]]$Paths)   # empty = the whole tree
    $pathPart = ''
    if ($Paths.Count -gt 0) { $pathPart = ' -- ' + (($Paths | ForEach-Object { '"' + $_ + '"' }) -join ' ') }
    $command = "git -C `"$repoRoot`" archive --format=tar $tag$pathPart | tar -xf - -C `"$targetFull`""
    & cmd.exe /c $command
    if ($LASTEXITCODE -ne 0) { throw "Copying files from Git failed (command: $command)." }
}

if ($Snapshot) {
    Expand-FromGit -Paths @()
}
else {
    for ($i = 0; $i -lt $toCopy.Count; $i += 40) {          # batches keep the command line short
        $count = [Math]::Min(40, $toCopy.Count - $i)
        Expand-FromGit -Paths $toCopy.GetRange($i, $count).ToArray()
    }
}

foreach ($path in $toRemove) {
    $full = Join-Path $targetFull ($path -replace '/', '\')
    if (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Force }
}

# --- what next -----------------------------------------------------------------------------------------
Write-Host ""
Write-Host "Done: $($toCopy.Count) file(s) copied, $($toRemove.Count) removed -> $targetFull" -ForegroundColor Green
if (Test-Path -LiteralPath (Join-Path $targetFull '.git')) {
    Write-Host ""
    Write-Host "git status in the target:" -ForegroundColor Cyan
    & git -C $targetFull status --short
}
Write-Host ""
Write-Host "Next: open the target, build (dotnet build) and test (dotnet test), then commit it yourself." -ForegroundColor Cyan
Write-Host "Original commit message of this step, for inspiration (write your own in your own words):" -ForegroundColor DarkGray
Write-Host "  $subject" -ForegroundColor DarkGray
$body | ForEach-Object { if ($_ -ne '') { Write-Host "  $_" -ForegroundColor DarkGray } }
