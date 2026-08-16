[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$PublishProfile = "Desktop",
    [string]$PublishDir = "",
    [string]$InstallerOutputDir = "",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "FinancialPlanningApp.Web\FinancialPlanningApp.Web.csproj"
$installerScript = Join-Path $repoRoot "installer\windows\DomesticFinancialPlanning.iss"

function Resolve-DeployRoot {
    if (-not [string]::IsNullOrWhiteSpace($env:FINANCIAL_PLANNING_DEPLOY_ROOT)) {
        return $env:FINANCIAL_PLANNING_DEPLOY_ROOT
    }

    return Join-Path (Split-Path -Parent $repoRoot) "DEPLOY\FinancialPlanning"
}

function Get-ProjectVersion {
    param([string]$Path)

    [xml]$project = Get-Content -LiteralPath $Path -Raw
    foreach ($group in $project.Project.PropertyGroup) {
        if ($group.Version) {
            return [string]$group.Version
        }
    }

    return "1.0.2.0"
}

function Resolve-InnoCompiler {
    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path $env:ProgramFiles "Inno Setup 7\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 7\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 7\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "ISCC.exe werd niet gevonden. Installeer Inno Setup 7 of 6 en voer dit script opnieuw uit. Via winget kan dat met: winget install JRSoftware.InnoSetup"
}

$deployRoot = Resolve-DeployRoot
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $deployRoot "Desktop"
}

if ([string]::IsNullOrWhiteSpace($InstallerOutputDir)) {
    $InstallerOutputDir = Join-Path $deployRoot "Installer"
}

if (-not $SkipPublish) {
    dotnet publish $projectPath -c $Configuration /p:PublishProfile=$PublishProfile
}

$forbiddenFiles = Get-ChildItem -LiteralPath $PublishDir -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -eq "secrets.json" -or
        ($_.Name -like "secrets.*.json" -and $_.Name -ne "secrets.template.json") -or
        $_.Name -eq "appsettings.Local.json"
    }
if ($forbiddenFiles) {
    $names = ($forbiddenFiles | ForEach-Object { $_.FullName }) -join [Environment]::NewLine
    throw "De publish-output bevat lokale secrets of lokale config en wordt niet verpakt:$([Environment]::NewLine)$names"
}

$version = Get-ProjectVersion -Path $projectPath
$iscc = Resolve-InnoCompiler
New-Item -ItemType Directory -Force -Path $InstallerOutputDir | Out-Null

$arguments = @(
    "/DProjectRoot=$repoRoot",
    "/DSourceDir=$PublishDir",
    "/DOutputDir=$InstallerOutputDir",
    "/DAppVersion=$version",
    $installerScript
)

& $iscc @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compiler eindigde met exitcode $LASTEXITCODE."
}

$installerPath = Join-Path $InstallerOutputDir "DomesticFinancialPlanning-Setup-$version.exe"
Write-Host "Installer klaar: $installerPath"
