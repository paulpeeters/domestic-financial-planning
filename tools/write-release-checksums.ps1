[CmdletBinding()]
param(
    [string]$Version = "1.0.3.0",
    [string]$InstallerPath = "",
    [string]$ServerZipPath = "",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
function Resolve-DeployRoot {
    if (-not [string]::IsNullOrWhiteSpace($env:FINANCIAL_PLANNING_DEPLOY_ROOT)) {
        return $env:FINANCIAL_PLANNING_DEPLOY_ROOT
    }

    return Join-Path (Split-Path -Parent $repoRoot) "DEPLOY\FinancialPlanning"
}

$deployRoot = Resolve-DeployRoot
if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
    $InstallerPath = Join-Path $deployRoot "Installer\DomesticFinancialPlanning-Setup-$Version.exe"
}

if ([string]::IsNullOrWhiteSpace($ServerZipPath)) {
    $ServerZipPath = Join-Path $deployRoot "Packages\DomesticFinancialPlanning-Server-$Version.zip"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "website\downloads\checksums.txt"
}

$items = @(
    @{
        Path = $InstallerPath
        Name = "DomesticFinancialPlanning-Setup-$Version.exe"
    },
    @{
        Path = $ServerZipPath
        Name = "DomesticFinancialPlanning-Server-$Version.zip"
    }
)

$lines = foreach ($item in $items) {
    if (-not (Test-Path -LiteralPath $item.Path)) {
        throw "Bestand niet gevonden: $($item.Path)"
    }

    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $item.Path
    "SHA256  $($item.Name)  $($hash.Hash)"
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$lines | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$lines
