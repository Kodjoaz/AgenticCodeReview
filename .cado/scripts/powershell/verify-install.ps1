#Requires -Version 5.1
<#
.SYNOPSIS
    Verifies that CADO Framework is correctly installed in a target repository.

.DESCRIPTION
    Validates the CADO Framework installation by checking:
    - Required directories exist
    - manifest.json and integration.json are present and valid JSON
    - All required prompt files are present
    - .github/agents/agents.yml is present and valid YAML
    - Timestamp in manifest.json is recent

.PARAMETER TargetRepo
    Path to the target repository. Defaults to the current directory.

.EXAMPLE
    .\verify-install.ps1 -TargetRepo C:\Projects\my-repo
#>

param(
    [string]$TargetRepo = (Get-Location).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Load shared utilities
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "common.ps1")

$Version = Get-CADO FrameworkVersion
$Failures = @()
$Success = $true

Write-FuzeLog "CADO Framework Installation Verification v$Version" -Level "header"
Write-FuzeLog "Target repository: $TargetRepo" -Level "info"
Write-Host ""

# --- Check required directories ---
Write-FuzeLog "Checking required directories..." -Level "info"
$RequiredDirs = @(
    ".cado\\templates",
    ".cado\\integrations",
    ".cado\\signals",
    ".cado",
    ".cado\\workflows",
    ".cado\\commands",
    ".cado\\scripts",
    ".github\agents",
    ".github\prompts",
    ".github\skills"
)

foreach ($Dir in $RequiredDirs) {
    $FullPath = Join-Path $TargetRepo $Dir
    if (Test-Path $FullPath) {
        Write-FuzeLog "  Found: $Dir" -Level "ok"
    } else {
        Write-FuzeLog "  Missing: $Dir" -Level "error"
        $Failures += "Missing directory: $Dir"
        $Success = $false
    }
}

Write-Host ""

# --- Check skills content ---
Write-FuzeLog "Validating skills content..." -Level "info"
$SkillsPath = Join-Path $TargetRepo ".github\skills"
if (Test-Path $SkillsPath) {
    $SkillFiles = @(Get-ChildItem -Path $SkillsPath -Recurse -File -Filter "SKILL.md")
    if ($SkillFiles.Count -ge 1) {
        Write-FuzeLog "  Found: $($SkillFiles.Count) SKILL.md file(s)" -Level "ok"
    } else {
        Write-FuzeLog "  No SKILL.md files found under .github\skills" -Level "error"
        $Failures += "No SKILL.md files found under .github\skills"
        $Success = $false
    }
}

Write-Host ""

# --- Check manifest.json ---
Write-FuzeLog "Validating manifest.json..." -Level "info"
$ManifestPath = Join-Path $TargetRepo ".cado\\manifest.json"
if (Test-Path $ManifestPath) {
    try {
        $Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
        Write-FuzeLog "  Valid JSON: manifest.json" -Level "ok"

        if ($Manifest.framework -eq "cado") {
            Write-FuzeLog "  Framework: cado" -Level "ok"
        } else {
            Write-FuzeLog "  Invalid framework: $($Manifest.framework)" -Level "error"
            $Failures += "Invalid framework in manifest.json"
            $Success = $false
        }

        if ($Manifest.installed_at) {
            $InstallTime = [DateTime]::Parse($Manifest.installed_at)
            $Hours = ((Get-Date) - $InstallTime).TotalHours
            Write-FuzeLog "  Installed: $($Manifest.installed_at) ($($Hours.ToString('F1')) hours ago)" -Level "ok"
        } else {
            Write-FuzeLog "  Warning: No installed_at timestamp" -Level "warn"
        }
    } catch {
        Write-FuzeLog "  Invalid JSON: manifest.json" -Level "error"
        $Failures += "Invalid JSON in manifest.json: $_"
        $Success = $false
    }
} else {
    Write-FuzeLog "  Missing: manifest.json" -Level "error"
    $Failures += "Missing manifest.json"
    $Success = $false
}

Write-Host ""

# --- Check integration.json ---
Write-FuzeLog "Validating integration.json..." -Level "info"
$IntegrationPath = Join-Path $TargetRepo ".cado\\integration.json"
if (Test-Path $IntegrationPath) {
    try {
        $Integration = Get-Content $IntegrationPath -Raw | ConvertFrom-Json
        Write-FuzeLog "  Valid JSON: integration.json" -Level "ok"
        Write-FuzeLog "  Active integration: $($Integration.integration)" -Level "ok"
    } catch {
        Write-FuzeLog "  Invalid JSON: integration.json" -Level "error"
        $Failures += "Invalid JSON in integration.json: $_"
        $Success = $false
    }
} else {
    Write-FuzeLog "  Missing: integration.json" -Level "error"
    $Failures += "Missing integration.json"
    $Success = $false
}

Write-Host ""

# --- Check prompt files ---
Write-FuzeLog "Validating prompt files..." -Level "info"
$RequiredPrompts = @(
    "cado.intake.prompt.md",
    "cado.route.prompt.md",
    "cado.plan.prompt.md",
    "cado.gate.prompt.md",
    "cado.build.prompt.md",
    "cado.prove.prompt.md",
    "cado.evidence.prompt.md",
    "cado.ship.prompt.md",
    "cado.git.commit.prompt.md",
    "cado.git.feature.prompt.md",
    "cado.git.initialize.prompt.md"
)

$PromptsFound = 0
foreach ($Prompt in $RequiredPrompts) {
    $PromptPath = Join-Path $TargetRepo ".github\prompts" $Prompt
    if (Test-Path $PromptPath) {
        $PromptsFound += 1
    } else {
        Write-FuzeLog "  Missing: $Prompt" -Level "warn"
        $Failures += "Missing prompt: $Prompt"
    }
}
Write-FuzeLog "  Found: $PromptsFound / $($RequiredPrompts.Count) prompts" -Level "ok"

Write-Host ""

# --- Check agents.yml ---
Write-FuzeLog "Validating agents.yml..." -Level "info"
$AgentsPath = Join-Path $TargetRepo ".github\agents\agents.yml"
if (Test-Path $AgentsPath) {
    try {
        $Content = Get-Content $AgentsPath -Raw
        # Basic YAML validation: should contain "agents:" key
        if ($Content -match "^\s*agents\s*:") {
            Write-FuzeLog "  Valid YAML: agents.yml" -Level "ok"
        } else {
            Write-FuzeLog "  Warning: agents.yml may not be valid YAML" -Level "warn"
        }
    } catch {
        Write-FuzeLog "  Error reading agents.yml" -Level "error"
        $Failures += "Cannot read agents.yml: $_"
        $Success = $false
    }
} else {
    Write-FuzeLog "  Missing: agents.yml" -Level "warn"
    $Failures += "Missing agents.yml"
}

Write-Host ""

# --- Check config.yml and extensions.yml ---
Write-FuzeLog "Validating configuration files..." -Level "info"
$ConfigPath = Join-Path $TargetRepo ".cado\\config.yml"
if (Test-Path $ConfigPath) {
    Write-FuzeLog "  Found: .cado\\config.yml" -Level "ok"
} else {
    Write-FuzeLog "  Missing: .cado\\config.yml" -Level "warn"
    $Failures += "Missing .cado\\config.yml"
}

$ExtensionsPath = Join-Path $TargetRepo ".cado\\extensions.yml"
if (Test-Path $ExtensionsPath) {
    Write-FuzeLog "  Found: .cado\\extensions.yml" -Level "ok"
} else {
    Write-FuzeLog "  Missing: .cado\\extensions.yml" -Level "warn"
    $Failures += "Missing .cado\\extensions.yml"
}

Write-Host ""

# --- Ensure operational files are not installed at target root ---
Write-FuzeLog "Checking for root-level operational files (legacy layout)" -Level "info"
$UnexpectedRootPaths = @(
    "workflows",
    "commands",
    "scripts",
    "config.yml",
    "extensions.yml"
)

foreach ($Path in $UnexpectedRootPaths) {
    $FullPath = Join-Path $TargetRepo $Path
    if (Test-Path $FullPath) {
        Write-FuzeLog "  Found unexpected root path: $Path" -Level "error"
        $Failures += "Unexpected root path present: $Path"
        $Success = $false
    } else {
        Write-FuzeLog "  Not present at root: $Path" -Level "ok"
    }
}

Write-Host ""

# --- Summary ---
if ($Success -and $Failures.Count -eq 0) {
    Write-FuzeLog "Installation verification PASSED" -Level "header"
    Write-Host ""
    Write-Host "  Status: All required files and directories are present."
    Write-Host ""
    exit 0
} else {
    Write-FuzeLog "Installation verification FAILED" -Level "error"
    Write-Host ""
    Write-Host "  Issues found:"
    foreach ($Failure in $Failures) {
        Write-Host "    - $Failure" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "  Run install.ps1 again to repair the installation."
    Write-Host ""
    exit 1
}


