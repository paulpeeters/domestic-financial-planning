[CmdletBinding()]
param(
    [string]$Version = "",
    [ValidateSet("Sftp", "Scp")]
    [string]$UploadMode = "Sftp",
    [string]$RemoteHost = "",
    [string]$RemoteUser = "",
    [string]$RemotePath = ".",
    [int]$Port = 22,
    [string]$IdentityFile = "",
    [switch]$SkipBuild,
    [switch]$SkipUpload,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "FinancialPlanningApp.Web\FinancialPlanningApp.Web.csproj"
$websiteRoot = Join-Path $repoRoot "website"
$stageRoot = Join-Path $repoRoot "artifacts\website-upload"

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

    return "1.0.4.0"
}

function Set-StagedServerDownloadMetadata {
    param(
        [string]$Path,
        [string]$Version,
        [string]$ServerZipPath
    )

    $content = Get-Content -LiteralPath $Path -Raw
    $downloadPattern = '/downloads/DomesticFinancialPlanning-Server-[^"'']+\.zip'
    $downloadReplacement = "/downloads/DomesticFinancialPlanning-Server-$Version.zip"
    $updated = [regex]::Replace($content, $downloadPattern, $downloadReplacement)

    if ($updated -eq $content -and $content -notmatch [regex]::Escape($downloadReplacement)) {
        throw "Geen serverdownloadlink gevonden in $Path."
    }

    $checksum = (Get-FileHash -LiteralPath $ServerZipPath -Algorithm SHA256).Hash
    $checksumPattern = '<p class="checksum">SHA-256: <code>[A-Fa-f0-9]{64}</code></p>'
    $checksumReplacement = "<p class=`"checksum`">SHA-256: <code>$checksum</code></p>"
    $updatedWithChecksum = [regex]::Replace($updated, $checksumPattern, $checksumReplacement)
    if ($updatedWithChecksum -eq $updated -and $updated -notmatch [regex]::Escape($checksumReplacement)) {
        throw "Geen serverchecksum gevonden in $Path."
    }

    Set-Content -LiteralPath $Path -Value $updatedWithChecksum -Encoding UTF8
}

function Remove-SafeDirectory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $artifactsRoot = (Resolve-Path -LiteralPath (Join-Path $repoRoot "artifacts")).Path
    if (-not $resolved.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Onveilige cleanup geweigerd: $resolved"
    }

    Remove-Item -LiteralPath $resolved -Recurse -Force
}

function Copy-RequiredFile {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Bestand niet gevonden: $Source"
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

$projectVersion = Get-ProjectVersion -Path $projectPath
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $projectVersion
}
elseif ($Version -ne $projectVersion) {
    throw "De opgegeven websiteversie ($Version) verschilt van de projectversie ($projectVersion)."
}

$deployRoot = Resolve-DeployRoot
$serverZipPath = Join-Path $deployRoot "Packages\DomesticFinancialPlanning-Server-$Version.zip"
$checksumsPath = Join-Path $websiteRoot "downloads\checksums.txt"

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "package-server-zip.ps1")
    & (Join-Path $PSScriptRoot "write-release-checksums.ps1") -Version $Version -ServerZipPath $serverZipPath -OutputPath $checksumsPath
}

Remove-SafeDirectory -Path $stageRoot
New-Item -ItemType Directory -Force -Path (Join-Path $stageRoot "updates") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stageRoot "downloads") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stageRoot "privacy") | Out-Null

Copy-RequiredFile -Source (Join-Path $websiteRoot "index.html") -Destination (Join-Path $stageRoot "index.html")
Set-StagedServerDownloadMetadata -Path (Join-Path $stageRoot "index.html") -Version $Version -ServerZipPath $serverZipPath
Copy-RequiredFile -Source (Join-Path $websiteRoot "privacy\index.html") -Destination (Join-Path $stageRoot "privacy\index.html")
Copy-RequiredFile -Source (Join-Path $websiteRoot "updates\latest.json") -Destination (Join-Path $stageRoot "updates\latest.json")
Copy-RequiredFile -Source $checksumsPath -Destination (Join-Path $stageRoot "downloads\checksums.txt")
Copy-RequiredFile -Source $serverZipPath -Destination (Join-Path $stageRoot "downloads\DomesticFinancialPlanning-Server-$Version.zip")

Write-Host "Website upload staging klaar: $stageRoot"

if ($SkipUpload) {
    Write-Host "Upload overgeslagen door -SkipUpload."
    return
}

if ([string]::IsNullOrWhiteSpace($RemoteHost)) {
    throw "Geef -RemoteHost mee, of gebruik -SkipUpload om alleen lokaal te stagen."
}

$sshArgs = @()
$scpArgs = @()
$sftpArgs = @()
if ($Port -ne 22) {
    $sshArgs += @("-p", [string]$Port)
    $scpArgs += @("-P", [string]$Port)
    $sftpArgs += @("-P", [string]$Port)
}
if (-not [string]::IsNullOrWhiteSpace($IdentityFile)) {
    $sshArgs += @("-i", $IdentityFile)
    $scpArgs += @("-i", $IdentityFile)
    $sftpArgs += @("-i", $IdentityFile)
}

$remoteLogin = if ([string]::IsNullOrWhiteSpace($RemoteUser)) { $RemoteHost } else { "$RemoteUser@$RemoteHost" }
$normalizedRemotePath = if ([string]::IsNullOrWhiteSpace($RemotePath)) { "." } else { $RemotePath.TrimEnd("/") }
$remoteTarget = "$remoteLogin`:$normalizedRemotePath/"
$stageContents = Join-Path $stageRoot "*"

Write-Host "Remote target: $remoteTarget"

if ($DryRun) {
    Write-Host "Dry run: zou uitvoeren:"
    if ($UploadMode -eq "Sftp") {
        Write-Host "sftp $($sftpArgs -join ' ') -b <generated-batch-file> $remoteLogin"
    }
    else {
        Write-Host "ssh $($sshArgs -join ' ') $remoteLogin mkdir -p '$normalizedRemotePath/downloads' '$normalizedRemotePath/updates'"
        Write-Host "scp $($scpArgs -join ' ') -r $stageContents $remoteTarget"
    }
    return
}

if ($UploadMode -eq "Sftp") {
    $batchPath = Join-Path $stageRoot "sftp-upload.batch"
    $commands = New-Object System.Collections.Generic.List[string]
    if ($normalizedRemotePath -ne ".") {
        $commands.Add("cd `"$normalizedRemotePath`"")
    }
    $commands.Add("-mkdir downloads")
    $commands.Add("-mkdir updates")
    $commands.Add("-mkdir privacy")
    $commands.Add("put `"$stageRoot\index.html`" `"index.html`"")
    $commands.Add("put `"$stageRoot\privacy\index.html`" `"privacy/index.html`"")
    $commands.Add("put `"$stageRoot\updates\latest.json`" `"updates/latest.json`"")
    $commands.Add("put `"$stageRoot\downloads\checksums.txt`" `"downloads/checksums.txt`"")
    $commands.Add("put `"$stageRoot\downloads\DomesticFinancialPlanning-Server-$Version.zip`" `"downloads/DomesticFinancialPlanning-Server-$Version.zip`"")
    $commands.Add("bye")
    $commands | Set-Content -LiteralPath $batchPath -Encoding ASCII

    & sftp @sftpArgs -b $batchPath $remoteLogin
    if ($LASTEXITCODE -ne 0) {
        throw "SFTP upload mislukte met exitcode $LASTEXITCODE."
    }

    Write-Host "Website upload klaar via SFTP: https://financialplanning.pware.be/"
    return
}

& ssh @sshArgs $remoteLogin "mkdir -p '$normalizedRemotePath/downloads' '$normalizedRemotePath/updates'"
if ($LASTEXITCODE -ne 0) {
    throw "Remote directory aanmaken mislukte met exitcode $LASTEXITCODE."
}

& scp @scpArgs -r $stageContents $remoteTarget
if ($LASTEXITCODE -ne 0) {
    throw "SCP upload mislukte met exitcode $LASTEXITCODE."
}

Write-Host "Website upload klaar via SCP: https://financialplanning.pware.be/"
