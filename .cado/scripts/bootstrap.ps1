<#
.SYNOPSIS
  Interactive bootstrap for CADO Framework.

.DESCRIPTION
  Prompts for specification framework, AI tool integration, Maximus persona,
  and execution profile, then initializes a target repository with CADO,
  writes selections to .cado/config.yml, and runs doctor verification.

.USAGE
  ./scripts/bootstrap.ps1 -TargetRepo C:\Path\To\Repo
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TargetRepo
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info([string]$Message) {
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Warn([string]$Message) {
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Err([string]$Message) {
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Resolve-TargetRepo([string]$PathValue) {
    try {
        return (Resolve-Path -LiteralPath $PathValue).Path
    }
    catch {
        throw "Target repository does not exist: $PathValue"
    }
}

function Assert-CommandExists([string]$CommandName) {
    $cmd = Get-Command $CommandName -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw "Required command not found on PATH: $CommandName"
    }
}

function Show-Header {
    Write-Host ''
    Write-Host '=========================================' -ForegroundColor DarkCyan
    Write-Host ' CADO Framework Interactive Bootstrap' -ForegroundColor Green
    Write-Host '=========================================' -ForegroundColor DarkCyan
    Write-Host ''
}

function Read-MenuSelection {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Title,
        [Parameter(Mandatory = $true)]
        [array]$Options
    )

    while ($true) {
        Write-Host $Title -ForegroundColor White
        for ($i = 0; $i -lt $Options.Count; $i++) {
            $num = $i + 1
            Write-Host "  $num) $($Options[$i].Label)"
        }

        $raw = Read-Host 'Enter selection number'
        $selectedIndex = 0
        if ([int]::TryParse($raw, [ref]$selectedIndex)) {
            if ($selectedIndex -ge 1 -and $selectedIndex -le $Options.Count) {
                return $Options[$selectedIndex - 1]
            }
        }

        Write-Warn 'Invalid selection. Please choose a valid number.'
        Write-Host ''
    }
}

function Resolve-IntegrationId([string]$ToolKey) {
    switch ($ToolKey) {
        'copilot' { return 'copilot' }
        'claude' { return 'claude' }
        'cursor' { return 'cursor' }
        'other' { return 'gemini' }
        default { return 'copilot' }
    }
}

function Resolve-PersonaProfile([string]$PersonaKey) {
    switch ($PersonaKey) {
        'architect' { return 'conductor' }
        'executor' { return 'maximus' }
        'facilitator' { return 'conductor' }
        default { return 'maximus' }
    }
}

function Resolve-DisplayName([string]$PersonaKey) {
    switch ($PersonaKey) {
        'architect' { return 'Architect' }
        'executor' { return 'Executor' }
        'facilitator' { return 'Facilitator' }
        default { return 'Maximus' }
    }
}

function Resolve-RuntimeProfile([string]$ExecutionKey) {
    switch ($ExecutionKey) {
        'express' { return 'precise' }
        'standard' { return 'balanced' }
        'full' { return 'creative' }
        default { return 'balanced' }
    }
}

function Update-CadoConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConfigPath,
        [Parameter(Mandatory = $true)]
        [string]$SpecFramework,
        [Parameter(Mandatory = $true)]
        [string]$Integration,
        [Parameter(Mandatory = $true)]
        [string]$PersonaProfile,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeProfile,
        [Parameter(Mandatory = $true)]
        [string]$DisplayName,
        [Parameter(Mandatory = $true)]
        [string]$PersonaChoice,
        [Parameter(Mandatory = $true)]
        [string]$ExecutionChoice,
        [Parameter(Mandatory = $true)]
        [string]$AiToolChoice
    )

    if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
        throw "Config file not found: $ConfigPath"
    }

    $lines = Get-Content -LiteralPath $ConfigPath
    $result = New-Object System.Collections.Generic.List[string]

    $section = ''
    $inConductor = $false

    foreach ($line in $lines) {
        if ($line -match '^[A-Za-z_][A-Za-z0-9_-]*:\s*$') {
            $section = ($line -replace ':\s*$', '')
            $inConductor = $false
        }

        if ($section -eq 'agent_identity' -and $line -match '^\s{2}conductor:\s*$') {
            $inConductor = $true
            $result.Add($line)
            continue
        }

        if ($section -eq 'agent_identity' -and $inConductor -and $line -match '^\s{2}[A-Za-z_][A-Za-z0-9_-]*:\s*$' -and $line -notmatch '^\s{2}conductor:\s*$') {
            $inConductor = $false
        }

        if ($section -eq 'spec_frameworks' -and $line -match '^\s*active:\s*') {
            $result.Add("  active: $SpecFramework")
            continue
        }

        if ($section -eq 'defaults' -and $line -match '^\s*integration:\s*') {
            $result.Add("  integration: $Integration")
            continue
        }

        if ($section -eq 'persona_profiles' -and $line -match '^\s*active_persona:\s*') {
            $result.Add("  active_persona: $PersonaProfile")
            continue
        }

        if ($section -eq 'runtime_profiles' -and $line -match '^\s*active_profile:\s*') {
            $result.Add("  active_profile: $RuntimeProfile")
            continue
        }

        if ($section -eq 'agent_identity' -and $inConductor -and $line -match '^\s{4}display_name:\s*') {
            $result.Add("    display_name: $DisplayName")
            continue
        }

        $result.Add($line)
    }

    $raw = ($result -join "`n") + "`n"
    $raw = [System.Text.RegularExpressions.Regex]::Replace(
        $raw,
        '(?ms)^bootstrap_selection:\n(?:^[ \t].*\n)*',
        ''
    )

    $timestamp = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ssK')
    $bootstrapBlock = @"
bootstrap_selection:
  spec_framework: $SpecFramework
  ai_tool: $AiToolChoice
  maximus_persona: $PersonaChoice
  execution_profile: $ExecutionChoice
  updated_at: $timestamp
"@

    $final = $raw.TrimEnd() + "`n`n" + $bootstrapBlock
    Set-Content -LiteralPath $ConfigPath -Value $final -Encoding UTF8
}

function Invoke-CadoInit {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedTarget,
        [Parameter(Mandatory = $true)]
        [string]$IntegrationId
    )

    Write-Info "Running cado init for target: $ResolvedTarget"
    & cado init --target $ResolvedTarget --integration $IntegrationId
    if ($LASTEXITCODE -ne 0) {
        throw "cado init failed with exit code $LASTEXITCODE"
    }
}

function Invoke-CadoDoctor {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedTarget
    )

    Write-Info "Running cado doctor for target: $ResolvedTarget"
    & cado doctor --target $ResolvedTarget
    if ($LASTEXITCODE -ne 0) {
        throw "cado doctor reported an unhealthy installation (exit $LASTEXITCODE)"
    }
}

try {
    Show-Header

    Assert-CommandExists -CommandName 'cado'
    $resolvedTarget = Resolve-TargetRepo -PathValue $TargetRepo

    $specChoice = Read-MenuSelection -Title 'Choose specification framework:' -Options @(
        [pscustomobject]@{ Key = 'spec-kit'; Label = 'Spec Kit (artifact-driven: spec.md, plan.md, tasks.md)' },
        [pscustomobject]@{ Key = 'openspec'; Label = 'OpenSpec (lightweight requirements)' },
        [pscustomobject]@{ Key = 'custom'; Label = 'Custom (use your own spec files and contract rules)' }
    )

    Write-Host ''
    $toolChoice = Read-MenuSelection -Title 'Choose AI tool integration:' -Options @(
        [pscustomobject]@{ Key = 'copilot'; Label = 'GitHub Copilot' },
        [pscustomobject]@{ Key = 'claude'; Label = 'Claude' },
        [pscustomobject]@{ Key = 'cursor'; Label = 'Cursor' },
        [pscustomobject]@{ Key = 'other'; Label = 'Other (mapped to Gemini profile)' }
    )

    Write-Host ''
    $personaChoice = Read-MenuSelection -Title 'Choose Maximus persona:' -Options @(
        [pscustomobject]@{ Key = 'architect'; Label = 'Architect' },
        [pscustomobject]@{ Key = 'executor'; Label = 'Executor' },
        [pscustomobject]@{ Key = 'facilitator'; Label = 'Facilitator' }
    )

    Write-Host ''
    $executionChoice = Read-MenuSelection -Title 'Choose execution profile:' -Options @(
        [pscustomobject]@{ Key = 'express'; Label = 'Express' },
        [pscustomobject]@{ Key = 'standard'; Label = 'Standard' },
        [pscustomobject]@{ Key = 'full'; Label = 'Full' }
    )

    $integrationId = Resolve-IntegrationId -ToolKey $toolChoice.Key
    $personaProfile = Resolve-PersonaProfile -PersonaKey $personaChoice.Key
    $displayName = Resolve-DisplayName -PersonaKey $personaChoice.Key
    $runtimeProfile = Resolve-RuntimeProfile -ExecutionKey $executionChoice.Key

    Write-Host ''
    Write-Info "Selected framework: $($specChoice.Key)"
    Write-Info "Selected tool: $($toolChoice.Key) -> integration '$integrationId'"
    Write-Info "Selected persona: $($personaChoice.Key)"
    Write-Info "Selected execution profile: $($executionChoice.Key) -> runtime '$runtimeProfile'"

    Invoke-CadoInit -ResolvedTarget $resolvedTarget -IntegrationId $integrationId

    $configPath = Join-Path $resolvedTarget '.cado/config.yml'
    Write-Info "Writing selections to: $configPath"
    Update-CadoConfig -ConfigPath $configPath `
        -SpecFramework $specChoice.Key `
        -Integration $integrationId `
        -PersonaProfile $personaProfile `
        -RuntimeProfile $runtimeProfile `
        -DisplayName $displayName `
        -PersonaChoice $personaChoice.Key `
        -ExecutionChoice $executionChoice.Key `
        -AiToolChoice $toolChoice.Key

    Invoke-CadoDoctor -ResolvedTarget $resolvedTarget

    Write-Host ''
    Write-Host 'Bootstrap completed successfully.' -ForegroundColor Green
    Write-Host "Target: $resolvedTarget"
    Write-Host "Config:  $configPath"
    exit 0
}
catch {
    Write-Err $_.Exception.Message
    exit 1
}
