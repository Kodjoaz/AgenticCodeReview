#Requires -Version 5.1
<#
.SYNOPSIS
    Checks prerequisites for CADO Framework installation.

.DESCRIPTION
    Validates that the target environment is ready for CADO Framework installation:
    - PowerShell version >= 5.1
    - Git is installed and available
    - Target directory exists and is writable
    - Target is a git repository (recommended)

.PARAMETER TargetRepo
    Path to the target repository. Defaults to the current directory.

.EXAMPLE
    .\check-prerequisites.ps1 -TargetRepo C:\Projects\my-repo
#>

param(
    [string]$TargetRepo = (Get-Location).Path,
    [switch]$Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Load shared utilities
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "common.ps1")

$Failures = @()
$Warnings = @()

Write-FuzeLog "CADO Framework Prerequisites Check" -Level "header"
Write-Host ""

# --- PowerShell version ---
Write-FuzeLog "Checking PowerShell version..." -Level "info"
$PSVersion = $PSVersionTable.PSVersion
if ($PSVersion.Major -ge 5 -and $PSVersion.Minor -ge 1) {
    Write-FuzeLog "  PowerShell: v$($PSVersion.Major).$($PSVersion.Minor)" -Level "ok"
} else {
    Write-FuzeLog "  PowerShell: v$($PSVersion.Major).$($PSVersion.Minor) (requires >= 5.1)" -Level "error"
    $Failures += "PowerShell version must be 5.1 or later"
}

Write-Host ""

# --- Git availability ---
Write-FuzeLog "Checking for Git..." -Level "info"
try {
    $GitVersion = (git --version 2>&1 | Select-Object -First 1)
    Write-FuzeLog "  $GitVersion" -Level "ok"
} catch {
    Write-FuzeLog "  Git not found in PATH" -Level "error"
    $Failures += "Git must be installed and available in PATH"
}

Write-Host ""

# --- Target directory ---
Write-FuzeLog "Checking target directory..." -Level "info"
if (-not (Test-Path $TargetRepo)) {
    Write-FuzeLog "  Directory does not exist: $TargetRepo" -Level "error"
    $Failures += "Target directory must exist"
} else {
    Write-FuzeLog "  Target: $TargetRepo" -Level "ok"

    # Check write permissions
    $TestFile = Join-Path $TargetRepo ".cadoflow-write-test-$([guid]::NewGuid())"
    try {
        [System.IO.File]::WriteAllText($TestFile, "test")
        Remove-Item $TestFile
        Write-FuzeLog "  Directory is writable" -Level "ok"
    } catch {
        Write-FuzeLog "  Directory is not writable" -Level "error"
        $Failures += "Target directory must be writable"
    }
}

Write-Host ""

# --- Git repository ---
Write-FuzeLog "Checking for git repository..." -Level "info"
$GitDir = Join-Path $TargetRepo ".git"
if (Test-Path $GitDir) {
    Write-FuzeLog "  Git repository found" -Level "ok"
} else {
    Write-FuzeLog "  .git directory not found (will still work, but recommended)" -Level "warn"
    $Warnings += "Target should be a git repository for full CADO Framework functionality"
}

Write-Host ""

# --- Summary ---
if ($Failures.Count -eq 0) {
    Write-FuzeLog "Prerequisites check PASSED" -Level "header"
    Write-Host ""
    Write-Host "  Status: Environment is ready for CADO Framework installation."
    Write-Host ""
    exit 0
} else {
    Write-FuzeLog "Prerequisites check FAILED" -Level "error"
    Write-Host ""
    Write-Host "  Issues found: $($Failures.Count) failure(s)"
    Write-Host ""
    $Failures | ForEach-Object { Write-Host "  - $_" }
    Write-Host ""
    exit 1
}

