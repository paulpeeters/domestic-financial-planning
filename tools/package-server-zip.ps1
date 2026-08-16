[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$PublishProfile = "Standard",
    [string]$PublishDir = "",
    [string]$PackageOutputDir = "",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "FinancialPlanningApp.Web\FinancialPlanningApp.Web.csproj"
$stageRoot = Join-Path $repoRoot "artifacts\server-zip-stage"

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

    return "1.0.3.0"
}

function Assert-NoPrivateConfig {
    param([string]$Path)

    $forbiddenFiles = Get-ChildItem -LiteralPath $Path -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -eq "secrets.json" -or
            ($_.Name -like "secrets.*.json" -and $_.Name -ne "secrets.template.json") -or
            $_.Name -eq "appsettings.Local.json"
        }

    if ($forbiddenFiles) {
        $names = ($forbiddenFiles | ForEach-Object { $_.FullName }) -join [Environment]::NewLine
        throw "De publish-output bevat lokale secrets of lokale config en wordt niet verpakt:$([Environment]::NewLine)$names"
    }
}

function Remove-PrivateConfig {
    param([string]$Path)

    $forbiddenFiles = Get-ChildItem -LiteralPath $Path -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -eq "secrets.json" -or
            ($_.Name -like "secrets.*.json" -and $_.Name -ne "secrets.template.json") -or
            $_.Name -eq "appsettings.Local.json" -or
            $_.Name -eq "appsettings.Development.json" -or
            $_.Name -eq "appsettings.Desktop.json" -or
            $_.Name -eq "desktop.mode"
        }

    foreach ($file in $forbiddenFiles) {
        Remove-Item -LiteralPath $file.FullName -Force
    }
}

function Copy-PackageFiles {
    param(
        [string]$Source,
        [string]$Destination
    )

    $excludedNames = @(
        "secrets.json",
        "appsettings.Local.json",
        "appsettings.Development.json",
        "appsettings.Desktop.json",
        "desktop.mode"
    )

    $excludedExtensions = @(
        ".pdb",
        ".map"
    )

    Get-ChildItem -LiteralPath $Source -Recurse -File | ForEach-Object {
        if ($excludedNames -contains $_.Name) {
            return
        }

        if ($_.Name -like "secrets.*.json" -and $_.Name -ne "secrets.template.json") {
            return
        }

        if ($excludedExtensions -contains $_.Extension) {
            return
        }

        $relativePath = [System.IO.Path]::GetRelativePath($Source, $_.FullName)
        $targetPath = Join-Path $Destination $relativePath
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetPath) | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $targetPath -Force
    }
}

$deployRoot = Resolve-DeployRoot
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $deployRoot "Server"
}

if ([string]::IsNullOrWhiteSpace($PackageOutputDir)) {
    $PackageOutputDir = Join-Path $deployRoot "Packages"
}

if (-not $SkipPublish) {
    dotnet publish $projectPath -c $Configuration /p:PublishProfile=$PublishProfile /p:PublishDir="$PublishDir\"
}

Remove-PrivateConfig -Path $PublishDir
Assert-NoPrivateConfig -Path $PublishDir

if (Test-Path -LiteralPath $stageRoot) {
    $resolvedStage = (Resolve-Path -LiteralPath $stageRoot).Path
    $expectedPrefix = (Resolve-Path -LiteralPath (Join-Path $repoRoot "artifacts")).Path
    if (-not $resolvedStage.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Onveilige stage cleanup geweigerd: $resolvedStage"
    }

    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null
Copy-PackageFiles -Source $PublishDir -Destination $stageRoot

$version = Get-ProjectVersion -Path $projectPath
New-Item -ItemType Directory -Force -Path $PackageOutputDir | Out-Null

$zipPath = Join-Path $PackageOutputDir "DomesticFinancialPlanning-Server-$version.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $stageRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "Server ZIP klaar: $zipPath"
