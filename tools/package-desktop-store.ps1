[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Version = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "FinancialPlanningApp.Web\FinancialPlanningApp.Web.csproj"
$packagingProject = Join-Path $repoRoot "FinancialPlanningApp.Wap\FinancialPlanningApp.Wap.wapproj"
$manifestPath = Join-Path $repoRoot "FinancialPlanningApp.Wap\Package.appxmanifest"
$packageRoot = Join-Path $repoRoot "FinancialPlanningApp.Wap\AppPackages"

function Get-ProjectVersion {
    param([string]$Path)

    [xml]$project = Get-Content -LiteralPath $Path -Raw
    foreach ($group in $project.Project.PropertyGroup) {
        if ($group.Version) {
            return [string]$group.Version
        }
    }

    throw "Geen Version gevonden in $Path."
}

function Resolve-MSBuild {
    $command = Get-Command "MSBuild.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        $resolved = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
            Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($resolved)) {
            return $resolved
        }
    }

    throw "MSBuild met de Windows Application Packaging-tools werd niet gevonden. Installeer de Visual Studio workload voor Windows application development."
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion -Path $projectPath
}

[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$manifestVersion = [string]$manifest.Package.Identity.Version
if ($manifestVersion -ne $Version) {
    throw "De MSIX-versie ($manifestVersion) verschilt van de applicatieversie ($Version). Werk beide versies gelijk bij voor de Store-build."
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "artifacts\store"
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$msbuild = Resolve-MSBuild
& $msbuild $packagingProject `
    /restore `
    /t:Rebuild `
    "/p:Configuration=$Configuration" `
    /p:Platform=x64 `
    /p:RuntimeIdentifier=win-x64 `
    /p:SelfContained=true `
    /p:OutputType=WinExe `
    /p:UapAppxPackageBuildMode=StoreUpload `
    /p:AppxBundle=Always `
    /p:AppxPackageSigningEnabled=false `
    /m `
    /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "De Store-packagebuild eindigde met exitcode $LASTEXITCODE."
}

$payloadRoot = Join-Path $repoRoot "FinancialPlanningApp.Wap\bin\x64\$Configuration\FinancialPlanningApp.Web"
$requiredPayload = @(
    (Join-Path $payloadRoot "FinancialPlanningApp.Web.exe"),
    (Join-Path $payloadRoot "coreclr.dll"),
    (Join-Path $payloadRoot "appsettings.Store.json")
)
foreach ($requiredFile in $requiredPayload) {
    if (-not (Test-Path -LiteralPath $requiredFile)) {
        throw "Verplicht bestand ontbreekt in de MSIX-payload: $requiredFile"
    }
}

$packageLayoutRoot = Join-Path $repoRoot "FinancialPlanningApp.Wap\obj\x64\$Configuration\PackageLayout\FinancialPlanningApp.Web"
$requiredPackageLayout = @(
    (Join-Path $packageLayoutRoot "wwwroot\css\site.css"),
    (Join-Path $packageLayoutRoot "wwwroot\js\site.js"),
    (Join-Path $packageLayoutRoot "wwwroot\lib\bootstrap\dist\css\bootstrap.min.css"),
    (Join-Path $packageLayoutRoot "wwwroot\lib\bootstrap\dist\js\bootstrap.bundle.min.js")
)
foreach ($requiredFile in $requiredPackageLayout) {
    if (-not (Test-Path -LiteralPath $requiredFile)) {
        throw "Verplicht statisch bestand ontbreekt in de MSIX package-layout: $requiredFile"
    }
}

$packageLayoutMarker = Join-Path $packageLayoutRoot "store.mode"
if (-not (Test-Path -LiteralPath $packageLayoutMarker)) {
    throw "De Store-modusmarker ontbreekt in de MSIX package-layout: $packageLayoutMarker"
}

$forbiddenFiles = Get-ChildItem -LiteralPath $payloadRoot -Recurse -File |
    Where-Object {
        $_.Name -eq "secrets.json" -or
        ($_.Name -like "secrets.*.json" -and $_.Name -ne "secrets.template.json") -or
        $_.Name -eq "appsettings.Local.json"
    }
if ($forbiddenFiles) {
    $names = ($forbiddenFiles | ForEach-Object FullName) -join [Environment]::NewLine
    throw "De MSIX-payload bevat lokale secrets of lokale configuratie:$([Environment]::NewLine)$names"
}

$upload = Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Filter "*.msixupload" |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if (-not $upload) {
    throw "De build slaagde, maar er werd geen .msixupload-bestand gevonden onder $packageRoot."
}

$destination = Join-Path $OutputDir "DomesticFinancialPlanning-Store-$Version-x64.msixupload"
Copy-Item -LiteralPath $upload.FullName -Destination $destination -Force
Write-Host "Store-upload klaar: $destination"
