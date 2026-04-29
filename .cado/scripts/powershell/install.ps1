#Requires -Version 5.1
<#
.SYNOPSIS
    Installs CADO Framework into a target repository.

.DESCRIPTION
    Copies all CADO Framework files into the target repository,
    writes manifest.json and integration.json with the current timestamp,
    and copies prompt files for the chosen integration.

.PARAMETER TargetRepo
    Path to the target repository. Defaults to the current directory.

.PARAMETER Integration
    AI integration to activate. One of: copilot, claude, cursor, gemini.
    Defaults to "copilot".

.PARAMETER Force
    Overwrite existing files without prompting.

.EXAMPLE
    .\install.ps1 -TargetRepo C:\Projects\my-repo -Integration copilot
#>

param(
    [string]$TargetRepo = (Get-Location).Path,
    [ValidateSet("copilot", "claude", "cursor", "gemini")]
    [string]$Integration = "copilot",
    [switch]$Force,
    [switch]$SkipPrerequisites
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Load shared utilities
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "common.ps1")

$CADO FrameworkRoot = Get-CADO FrameworkRoot
$Version = Get-CADO FrameworkVersion

# --- Optional prerequisite check ---
if (-not $SkipPrerequisites) {
    Write-FuzeLog "Running prerequisite check..." -Level "info"
    $PrereqScript = Join-Path $ScriptDir "check-prerequisites.ps1"
    if (Test-Path $PrereqScript) {
        & $PrereqScript -TargetRepo $TargetRepo
        if ($LASTEXITCODE -ne 0) {
            Write-FuzeLog "Prerequisites check failed. Aborting installation." -Level "error"
            exit 1
        }
    }
    Write-Host ""
}

Write-FuzeLog "CADO Framework Installer v$Version" -Level "header"
Write-FuzeLog "Target repository : $TargetRepo" -Level "info"
Write-FuzeLog "Integration       : $Integration" -Level "info"
Write-Host ""

# --- Validate target ---
if (-not (Test-Path $TargetRepo)) {
    Write-FuzeLog "Target path does not exist: $TargetRepo" -Level "error"
    exit 1
}

$TargetGit = Join-Path $TargetRepo ".git"
if (-not (Test-Path $TargetGit)) {
    Write-FuzeLog "WARNING: Target does not appear to be a git repository (.git not found)." -Level "warn"
}

function Copy-FuzeDir {
    param([string]$Source, [string]$Destination)
    if (-not (Test-Path $Destination)) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    }
    Copy-Item -Path (Join-Path $Source "*") -Destination $Destination -Recurse -Force:$Force
    Write-FuzeLog "Copied: $($Destination.Replace($TargetRepo, '.'))" -Level "ok"
}

function Copy-FuzeFile {
    param([string]$Source, [string]$Destination)
    $DestDir = Split-Path -Parent $Destination
    if (-not (Test-Path $DestDir)) {
        New-Item -ItemType Directory -Path $DestDir -Force | Out-Null
    }
    Copy-Item -Path $Source -Destination $Destination -Force:$Force
    Write-FuzeLog "Copied: $($Destination.Replace($TargetRepo, '.'))" -Level "ok"
}

# --- Copy framework directories from the CADO Framework source tree ---
Write-FuzeLog "Installing framework files..." -Level "info"

$Dirs = @(
    @{ Src = Join-Path $CADO FrameworkRoot ".cado\\templates";    Dst = Join-Path $TargetRepo ".cado\\templates" },
    @{ Src = Join-Path $CADO FrameworkRoot "src\workflows";          Dst = Join-Path $TargetRepo ".cado\\workflows" },
    @{ Src = Join-Path $CADO FrameworkRoot "commands";               Dst = Join-Path $TargetRepo ".cado\\commands" },
    @{ Src = Join-Path $CADO FrameworkRoot ".cado\\integrations"; Dst = Join-Path $TargetRepo ".cado\\integrations" },
    @{ Src = Join-Path $CADO FrameworkRoot "scripts";                Dst = Join-Path $TargetRepo ".cado\\scripts" },
    @{ Src = Join-Path $CADO FrameworkRoot "src\workflow";  Dst = Join-Path $TargetRepo ".cado\\workflow" },
    @{ Src = Join-Path $CADO FrameworkRoot "src\templates"; Dst = Join-Path $TargetRepo ".cado\\templates" },
    @{ Src = Join-Path $CADO FrameworkRoot "src\schemas";   Dst = Join-Path $TargetRepo ".cado\\schemas" },
    @{ Src = Join-Path $CADO FrameworkRoot "src\examples";  Dst = Join-Path $TargetRepo ".cado\\examples" },
    @{ Src = Join-Path $CADO FrameworkRoot "src\agents";    Dst = Join-Path $TargetRepo ".github\agents" },
    @{ Src = Join-Path $CADO FrameworkRoot "src\skills";    Dst = Join-Path $TargetRepo ".github\skills" }
)

foreach ($Dir in $Dirs) {
    if (Test-Path $Dir.Src) {
        Copy-FuzeDir -Source $Dir.Src -Destination $Dir.Dst
    }
}

# --- Copy prompt files ---
Write-FuzeLog "Installing prompt files..." -Level "info"
$PromptsSource = Join-Path $CADO FrameworkRoot "src\prompts"
$PromptsTarget = Join-Path $TargetRepo ".github\prompts"

if (-not (Test-Path $PromptsTarget)) {
    New-Item -ItemType Directory -Path $PromptsTarget -Force | Out-Null
}

# --- Copy framework README ---
$FrameworkReadmeSrc = Join-Path $CADO FrameworkRoot "src\README.md"
$FrameworkReadmeDst = Join-Path $TargetRepo ".cado\\README.md"
if (Test-Path $FrameworkReadmeSrc) {
    Copy-FuzeFile -Source $FrameworkReadmeSrc -Destination $FrameworkReadmeDst
}

Get-ChildItem -Path $PromptsSource -Filter "cado.*.prompt.md" | ForEach-Object {
    $Dest = Join-Path $PromptsTarget $_.Name
    Copy-Item -Path $_.FullName -Destination $Dest -Force:$Force
    Write-FuzeLog "Copied: .github\prompts\$($_.Name)" -Level "ok"
}

# --- Write manifest.json ---
Write-FuzeLog "Writing manifest.json..." -Level "info"
$Timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
$ManifestPath = Join-Path $TargetRepo ".cado\\manifest.json"

$Manifest = [ordered]@{
    framework    = "cado"
    version      = $Version
    description  = "Stage-based multi-agent delivery framework"
    installed_at = $Timestamp
    homepage     = "https://github.com/Keayoub/cado"
    requires     = [ordered]@{ min_version = "0.1.0" }
    files        = @{}
}
$Manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $ManifestPath -Encoding UTF8
Write-FuzeLog "Written: .cado\\manifest.json" -Level "ok"

# --- Write integration.json ---
$IntegrationPath = Join-Path $TargetRepo ".cado\\integration.json"
[ordered]@{
    integration = $Integration
    version     = $Version
} | ConvertTo-Json | Set-Content -Path $IntegrationPath -Encoding UTF8
Write-FuzeLog "Written: .cado\\integration.json (integration=$Integration)" -Level "ok"

# --- Copy extensions.yml (from root) ---
$ExtSrc = Join-Path $CADO FrameworkRoot "extensions.yml"
$ExtDst = Join-Path $TargetRepo ".cado\\extensions.yml"
if (Test-Path $ExtSrc) {
    Copy-FuzeFile -Source $ExtSrc -Destination $ExtDst
    Write-FuzeLog "Written: .cado\\extensions.yml" -Level "ok"
}

# --- Copy config.yml (from root) ---
$ConfigSrc = Join-Path $CADO FrameworkRoot "config.yml"
$ConfigDst = Join-Path $TargetRepo ".cado\\config.yml"
if (Test-Path $ConfigSrc) {
    Copy-FuzeFile -Source $ConfigSrc -Destination $ConfigDst
    Write-FuzeLog "Written: .cado\\config.yml" -Level "ok"
}

# --- Update workflow-registry timestamps ---
$RegistryPath = Join-Path $TargetRepo ".cado\\workflows\workflow-registry.json"
if (Test-Path $RegistryPath) {
    $Registry = Get-Content $RegistryPath -Raw | ConvertFrom-Json
    $Registry.workflows.cadoflow.installed_at = $Timestamp
    $Registry.workflows.cadoflow.updated_at   = $Timestamp
    $Registry | ConvertTo-Json -Depth 5 | Set-Content -Path $RegistryPath -Encoding UTF8
    Write-FuzeLog "Updated: .cado\\workflows\workflow-registry.json timestamps" -Level "ok"
}

# --- Initialize signals directory ---
Write-FuzeLog "Initializing signals directory..." -Level "info"
$SignalsPath = Join-Path $TargetRepo ".cado\\signals"
if (-not (Test-Path $SignalsPath)) {
    New-Item -ItemType Directory -Path $SignalsPath -Force | Out-Null
    Write-FuzeLog "Created: .cado\\signals" -Level "ok"
} else {
    Write-FuzeLog "Signals directory already exists" -Level "ok"
}

Write-Host ""

# --- Run post-install verification ---
Write-FuzeLog "Verifying installation..." -Level "info"
$VerifyScript = Join-Path $ScriptDir "verify-install.ps1"
if (Test-Path $VerifyScript) {
    & $VerifyScript -TargetRepo $TargetRepo
    if ($LASTEXITCODE -ne 0) {
        Write-FuzeLog "Installation verification FAILED" -Level "error"
        Write-Host ""
        exit 1
    }
} else {
    Write-FuzeLog "Verification script not found (skipped)" -Level "warn"
}

# --- Done ---
Write-Host ""
Write-FuzeLog "CADO Framework $Version installed successfully." -Level "header"
Write-Host ""
Write-Host "  INSTALLATION SUMMARY"
Write-Host "  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
Write-Host "  Framework Version     : $Version"
Write-Host "  Target Repository     : $TargetRepo"
Write-Host "  Active Integration    : $Integration"
Write-Host "  Installed At          : $Timestamp"
Write-Host ""
Write-Host "  DIRECTORIES INSTALLED"
Write-Host "  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
Write-Host "  .cado/workflows → Workflow definitions"
Write-Host "  .cado/commands  → Command registry"
Write-Host "  .cado/scripts   → Installation and utility scripts"
Write-Host ""
Write-Host "  .cado/templates        → Templates for change requests"
Write-Host "  .cado/signals          → Inter-process signals"
Write-Host "  .cado/integrations     → Integration-specific config"
Write-Host ""
Write-Host "  .github/agents             → Agent definitions"
Write-Host "  .github/prompts            → AI prompts for each stage"
Write-Host "  .cado           → Framework documentation"
Write-Host "  .github/skills             → Specialized skills library"
Write-Host ""
Write-Host "  CONFIGURATION FILES"
Write-Host "  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
Write-Host "  .cado/config.yml     → Default behavior settings"
Write-Host "  .cado/extensions.yml → Hooks and extensions"
Write-Host "  .cado/manifest.json    → Installation metadata"
Write-Host "  .cado/integration.json → Active integration setting"
Write-Host "  .cado/commands/commands.yml → Command registry"
Write-Host ""
Write-Host "  NEXT STEPS"
Write-Host "  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
Write-Host "   1. Customize constitution:"
Write-Host "      Edit .cado/templates/constitution-template.md"
Write-Host ""
Write-Host "   2. Review configuration:"
Write-Host "      Edit .cado/config.yml (timeouts, evidence requirements)"
Write-Host ""
Write-Host "   3. Run your first delivery:"
Write-Host "      Use the 'cado.intake' command in your AI tool"
Write-Host ""
Write-Host "   4. Read the documentation:"
Write-Host "      See .cado/README.md for detailed guidance"
Write-Host ""
Write-Host "  For help, run: Get-Help .\.cado\scripts\powershell\install.ps1 -Full"
Write-Host ""




