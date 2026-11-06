[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $root 'FinancialPlanningApp.Web\wwwroot\img\app-icon-512.png'
$outputDirectory = Join-Path $root 'FinancialPlanningApp.Wap\Images'

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Bronicoon niet gevonden: $sourcePath"
}

function New-Canvas {
    param(
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$Color
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear($Color)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

    return @{ Bitmap = $bitmap; Graphics = $graphics }
}

function Save-SquareAsset {
    param(
        [System.Drawing.Image]$Source,
        [string]$Name,
        [int]$Size
    )

    $canvas = New-Canvas $Size $Size ([System.Drawing.Color]::Transparent)
    try {
        $canvas.Graphics.DrawImage($Source, 0, 0, $Size, $Size)
        $canvas.Bitmap.Save((Join-Path $outputDirectory $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $canvas.Graphics.Dispose()
        $canvas.Bitmap.Dispose()
    }
}

function Save-LandscapeAsset {
    param(
        [System.Drawing.Image]$Source,
        [string]$Name,
        [int]$Width,
        [int]$Height,
        [int]$IconSize,
        [System.Drawing.Color]$Background
    )

    $canvas = New-Canvas $Width $Height $Background
    try {
        $x = [int](($Width - $IconSize) / 2)
        $y = [int](($Height - $IconSize) / 2)
        $canvas.Graphics.DrawImage($Source, $x, $y, $IconSize, $IconSize)
        $canvas.Bitmap.Save((Join-Path $outputDirectory $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $canvas.Graphics.Dispose()
        $canvas.Bitmap.Dispose()
    }
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$source = [System.Drawing.Image]::FromFile($sourcePath)
try {
    Save-SquareAsset $source 'StoreLogo.png' 50
    Save-SquareAsset $source 'Square44x44Logo.scale-200.png' 88
    Save-SquareAsset $source 'Square44x44Logo.targetsize-24_altform-unplated.png' 24
    Save-SquareAsset $source 'Square150x150Logo.scale-200.png' 300
    Save-SquareAsset $source 'LockScreenLogo.scale-200.png' 48
    Save-LandscapeAsset $source 'Wide310x150Logo.scale-200.png' 620 300 240 ([System.Drawing.ColorTranslator]::FromHtml('#F6FCFC'))
    Save-LandscapeAsset $source 'SplashScreen.scale-200.png' 1240 600 360 ([System.Drawing.ColorTranslator]::FromHtml('#F6FCFC'))
}
finally {
    $source.Dispose()
}

Get-ChildItem -LiteralPath $outputDirectory -Filter '*.png' |
    Sort-Object Name |
    Select-Object Name, Length
