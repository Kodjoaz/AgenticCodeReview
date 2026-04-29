<#
.SYNOPSIS
    Shared utility functions for CADO Framework PowerShell scripts.

.DESCRIPTION
    Import this file in other CADO Framework scripts using dot-sourcing:
        . (Join-Path $PSScriptRoot "common.ps1")
#>

#region Version

function Get-CADO FrameworkVersion {
    <#
    .SYNOPSIS
        Returns the installed CADO Framework version string by reading the VERSION file.
    #>
    $Root = Get-CADO FrameworkRoot
    $VersionFile = Join-Path $Root "VERSION"
    if (Test-Path $VersionFile) {
        return (Get-Content $VersionFile -Raw).Trim()
    }
    return "0.0.0"
}

#endregion

#region Root resolution

function Get-CADO FrameworkRoot {
    <#
    .SYNOPSIS
        Returns the absolute path to the CADO Framework distribution root.
    .DESCRIPTION
        Walks up from the scripts/powershell/ directory one level to reach
        the scripts/ directory, then one more level to reach the repo root.
        Adjust the depth constant if the script tree changes.
    #>
    # scripts/powershell -> scripts -> repo root
    $ScriptDir = Split-Path -Parent $PSScriptRoot
    $RepoRoot = Split-Path -Parent $ScriptDir   # repo root
    return $RepoRoot
}

#endregion

#region Logging

$Script:LogLevelColors = @{
    header = "Cyan"
    info   = "White"
    ok     = "Green"
    warn   = "Yellow"
    error  = "Red"
    debug  = "DarkGray"
}

function Write-FuzeLog {
    <#
    .SYNOPSIS
        Writes a colored log message with a [CADO Framework] prefix.

    .PARAMETER Message
        The message to display.

    .PARAMETER Level
        One of: header, info, ok, warn, error, debug. Defaults to "info".
    #>
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [ValidateSet("header", "info", "ok", "warn", "error", "debug")]
        [string]$Level = "info"
    )

    $Color = $Script:LogLevelColors[$Level]
    $Prefix = switch ($Level) {
        "header" { "[CADO Framework]" }
        "ok"     { "  [OK]    " }
        "warn"   { "  [WARN]  " }
        "error"  { "  [ERROR] " }
        "debug"  { "  [DEBUG] " }
        default  { "  [INFO]  " }
    }

    Write-Host "$Prefix $Message" -ForegroundColor $Color
}

#endregion

#region Installation checks

function Test-CADO FrameworkInstalled {
    <#
    .SYNOPSIS
        Returns $true if CADO Framework is installed in the specified target directory.

    .PARAMETER TargetRepo
        Path to check. Defaults to current directory.
    #>
    param(
        [string]$TargetRepo = (Get-Location).Path
    )

    $ManifestPath = Join-Path $TargetRepo ".cado" "manifest.json"
    return (Test-Path $ManifestPath)
}

function Get-CADO FrameworkInstalledVersion {
    <#
    .SYNOPSIS
        Returns the version string recorded in the target repo's manifest.json.
        Returns $null if CADO Framework is not installed.

    .PARAMETER TargetRepo
        Path to the target repository.
    #>
    param(
        [string]$TargetRepo = (Get-Location).Path
    )

    $ManifestPath = Join-Path $TargetRepo ".cado" "manifest.json"
    if (-not (Test-Path $ManifestPath)) {
        return $null
    }
    try {
        $Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
        return $Manifest.version
    } catch {
        return $null
    }
}

#endregion

