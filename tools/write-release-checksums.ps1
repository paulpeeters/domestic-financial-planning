[CmdletBinding()]
param(
    [string]$Version = "1.0.1.0",
    [string]$InstallerPath = "C:\DATA\Projects\DEPLOY\FinancialPlanning\Installer\DomesticFinancialPlanning-Setup-1.0.1.0.exe",
    [string]$ServerZipPath = "C:\DATA\Projects\DEPLOY\FinancialPlanning\Packages\DomesticFinancialPlanning-Server-1.0.1.0.zip",
    [string]$OutputPath = "C:\DATA\Projects\FinancialPlanning\website\downloads\checksums.txt"
)

$ErrorActionPreference = "Stop"

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
