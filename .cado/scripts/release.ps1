<#
.SYNOPSIS
  Release helper for CADO Framework.

.DESCRIPTION
  Sets an explicit version, commits, tags, optionally pushes, optionally runs
  build checks, and can optionally create a GitHub release via gh CLI.

.USAGE
  ./scripts/release.ps1 -NewVersion 0.2.0
  ./scripts/release.ps1 -NewVersion 0.2.0 -Push -Build
  ./scripts/release.ps1 -NewVersion 0.2.0 -Push -CreateGitHubRelease
#>

param(
    [Parameter(Mandatory=$true, Position=0)][string]$NewVersion,
    [switch]$Push,
    [switch]$Build,
    [switch]$CreateGitHubRelease,
    [switch]$Force,
    [string]$BaseBranch = "main"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Info([string]$m) { Write-Host "[INFO] $m" -ForegroundColor Cyan }
function Write-Warn([string]$m) { Write-Host "[WARN] $m" -ForegroundColor Yellow }
function Write-Err([string]$m) { Write-Host "[ERROR] $m" -ForegroundColor Red }

if (-not ($NewVersion -match '^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9\.]+)?$')) {
    Write-Err "Invalid version format: $NewVersion"
    exit 2
}

try { git --version | Out-Null } catch { Write-Err "git is not available on PATH"; exit 3 }

$repoRoot = (git rev-parse --show-toplevel) 2>$null
if ($LASTEXITCODE -ne 0 -or -not $repoRoot) {
    Write-Err "Not inside a git repository."
    exit 4
}
$repoRoot = $repoRoot.Trim()
Set-Location $repoRoot

$status = git status --porcelain
if ($status -and -not $Force) {
    Write-Err "Working tree is not clean. Commit/stash changes or use -Force."
    git status --short
    exit 5
}

$versionFile = Join-Path $repoRoot "VERSION"
$pyproject = Join-Path $repoRoot "pyproject.toml"
if (-not (Test-Path $versionFile -PathType Leaf) -or -not (Test-Path $pyproject -PathType Leaf)) {
    Write-Err "VERSION or pyproject.toml not found in repository root."
    exit 6
}

$oldVersion = (Get-Content -Raw $versionFile).Trim()
if ($oldVersion -eq $NewVersion) {
    Write-Warn "Version already set to $NewVersion."
}

Write-Info "Setting version: $oldVersion -> $NewVersion"
& python scripts/bump-version.py --set-version $NewVersion
if ($LASTEXITCODE -ne 0) {
    Write-Err "Version bump script failed."
    exit 7
}

if ($Build) {
    Write-Info "Running build checks..."
    & uv build
    if ($LASTEXITCODE -ne 0) { Write-Err "uv build failed"; exit 8 }

    & uv run cado --help | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Err "CLI sanity check failed"; exit 9 }
}

Write-Info "Staging version files..."
git add VERSION pyproject.toml
if ($LASTEXITCODE -ne 0) { Write-Err "git add failed"; exit 10 }

$commitMessage = "chore: bump version to $NewVersion"
Write-Info "Committing: $commitMessage"
git commit -m $commitMessage
if ($LASTEXITCODE -ne 0) { Write-Err "git commit failed"; exit 11 }

$tagName = "v$NewVersion"
Write-Info "Creating annotated tag: $tagName"
git tag -a $tagName -m "Release $tagName"
if ($LASTEXITCODE -ne 0) { Write-Err "git tag failed"; exit 12 }

if ($Push) {
    Write-Info "Pushing commit to origin/$BaseBranch"
    git push origin $BaseBranch
    if ($LASTEXITCODE -ne 0) { Write-Err "git push failed"; exit 13 }

    Write-Info "Pushing tag $tagName"
    git push origin $tagName
    if ($LASTEXITCODE -ne 0) { Write-Err "git push tag failed"; exit 14 }
}

if ($CreateGitHubRelease) {
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) {
        Write-Err "gh CLI not found. Install GitHub CLI or omit -CreateGitHubRelease."
        exit 15
    }

    $notes = "Automated release for $tagName. See GitHub workflow artifacts for details."
    Write-Info "Creating GitHub release for $tagName"
    gh release create $tagName --title "CADO Framework $tagName" --notes $notes
    if ($LASTEXITCODE -ne 0) { Write-Err "gh release create failed"; exit 16 }
}

Write-Info "Release workflow completed: $tagName"
Write-Host ""
Write-Host "Next:"
Write-Host "  - GitHub Release workflow runs on tag push"
Write-Host "  - PyPI publish can be enabled later via workflow + trusted publishing"

exit 0
