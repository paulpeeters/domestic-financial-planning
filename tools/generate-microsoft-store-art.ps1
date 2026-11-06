[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $root 'assets\microsoft-store'
$boxPath = Join-Path $outputDirectory 'domestic-financial-planning-box-art-2160x2160.png'
$posterPath = Join-Path $outputDirectory 'domestic-financial-planning-poster-art-1440x2160.png'

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Pen {
    param(
        [System.Drawing.Color]$Color,
        [float]$Width
    )

    $pen = [System.Drawing.Pen]::new($Color, $Width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function Draw-AppIcon {
    param(
        [System.Drawing.Graphics]$Graphics,
        [float]$X,
        [float]$Y,
        [float]$Size
    )

    $s = $Size / 512.0
    function P([float]$Value) { return $Value * $s }
    function SX([float]$Value) { return $X + (P $Value) }
    function SY([float]$Value) { return $Y + (P $Value) }

    $tilePath = New-RoundedRectanglePath (SX 40) (SY 40) (P 432) (P 432) (P 104)
    $tileBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#176076'))
    $Graphics.FillPath($tileBrush, $tilePath)

    $savedState = $Graphics.Save()
    $Graphics.SetClip($tilePath)
    $accentBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#259a7f'))
    $Graphics.FillPie($accentBrush, (SX 168), (SY 238), (P 416), (P 416), 180, 90)
    $Graphics.Restore($savedState)

    $sheetPath = New-RoundedRectanglePath (SX 112) (SY 120) (P 288) (P 266) (P 34)
    $sheetBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#f6fcfc'))
    $Graphics.FillPath($sheetBrush, $sheetPath)

    $savedState = $Graphics.Save()
    $Graphics.SetClip($sheetPath)
    $headerBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#d8efeb'))
    $Graphics.FillRectangle($headerBrush, (SX 112), (SY 120), (P 288), (P 68))
    $Graphics.Restore($savedState)

    $outlinePen = New-Pen ([System.Drawing.Color]::FromArgb(120, 13, 72, 92)) (P 4)
    $Graphics.DrawPath($outlinePen, $sheetPath)

    $whitePen = New-Pen ([System.Drawing.Color]::White) (P 18)
    $tealPen = New-Pen ([System.Drawing.ColorTranslator]::FromHtml('#176076')) (P 6)
    foreach ($ringX in @(178, 334)) {
        $Graphics.DrawLine($whitePen, (SX $ringX), (SY 94), (SX $ringX), (SY 150))
        $Graphics.DrawLine($tealPen, (SX $ringX), (SY 108), (SX $ringX), (SY 150))
    }

    $gridPen = New-Pen ([System.Drawing.Color]::FromArgb(150, 80, 125, 137)) (P 6)
    foreach ($gridX in @(186, 256, 326)) {
        $Graphics.DrawLine($gridPen, (SX $gridX), (SY 214), (SX $gridX), (SY 340))
    }
    foreach ($gridY in @(254, 300)) {
        $Graphics.DrawLine($gridPen, (SX 150), (SY $gridY), (SX 362), (SY $gridY))
    }

    $checkPoints = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new((SX 164), (SY 332)),
        [System.Drawing.PointF]::new((SX 218), (SY 278)),
        [System.Drawing.PointF]::new((SX 286), (SY 330)),
        [System.Drawing.PointF]::new((SX 352), (SY 244))
    )
    $checkWhitePen = New-Pen ([System.Drawing.Color]::White) (P 18)
    $checkTealPen = New-Pen ([System.Drawing.ColorTranslator]::FromHtml('#176076')) (P 9)
    $Graphics.DrawLines($checkWhitePen, $checkPoints)
    $Graphics.DrawLines($checkTealPen, $checkPoints)

    $coinBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#ecb84a'))
    $coinOutlinePen = New-Pen ([System.Drawing.Color]::FromArgb(130, 153, 111, 36)) (P 5)
    $Graphics.FillEllipse($coinBrush, (SX 314), (SY 310), (P 124), (P 124))
    $Graphics.DrawEllipse($coinOutlinePen, (SX 314), (SY 310), (P 124), (P 124))

    $coinDetailPen = New-Pen ([System.Drawing.Color]::FromArgb(230, 255, 244, 210)) (P 8)
    $Graphics.DrawEllipse($coinDetailPen, (SX 342), (SY 338), (P 68), (P 68))
    $coinLinePen = New-Pen ([System.Drawing.Color]::FromArgb(230, 255, 244, 210)) (P 7)
    $Graphics.DrawLine($coinLinePen, (SX 376), (SY 334), (SX 376), (SY 410))

    foreach ($item in @(
        $tilePath, $tileBrush, $accentBrush, $sheetPath, $sheetBrush, $headerBrush,
        $outlinePen, $whitePen, $tealPen, $gridPen, $checkWhitePen, $checkTealPen,
        $coinBrush, $coinOutlinePen, $coinDetailPen, $coinLinePen
    )) {
        $item.Dispose()
    }
}

function New-StoreCanvas {
    param(
        [int]$Width,
        [int]$Height,
        [string]$TopColor,
        [string]$BottomColor
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $rectangle = [System.Drawing.RectangleF]::new(0, 0, $Width, $Height)
    $gradient = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        $rectangle,
        [System.Drawing.ColorTranslator]::FromHtml($TopColor),
        [System.Drawing.ColorTranslator]::FromHtml($BottomColor),
        90
    )
    $graphics.FillRectangle($gradient, $rectangle)
    $gradient.Dispose()
    return @{ Bitmap = $bitmap; Graphics = $graphics }
}

function Add-SoftPattern {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [int]$Opacity
    )

    $linePen = New-Pen ([System.Drawing.Color]::FromArgb($Opacity, 255, 255, 255)) 3
    for ($offset = -$Height; $offset -lt $Width; $offset += 180) {
        $Graphics.DrawLine($linePen, $offset, 0, $offset + $Height, $Height)
    }

    $ringPen = New-Pen ([System.Drawing.Color]::FromArgb(($Opacity + 12), 236, 184, 74)) 8
    foreach ($ring in @(
        @(120, 210, 46),
        @(($Width - 126), 320, 74),
        @(176, ($Height - 230), 86),
        @(($Width - 170), ($Height - 180), 42)
    )) {
        $Graphics.DrawEllipse($ringPen, $ring[0] - $ring[2], $ring[1] - $ring[2], $ring[2] * 2, $ring[2] * 2)
    }

    $linePen.Dispose()
    $ringPen.Dispose()
}

function Save-BoxArt {
    param([string]$Path)

    $canvas = New-StoreCanvas 2160 2160 '#f6fcfc' '#dcefeb'
    try {
        Add-SoftPattern $canvas.Graphics 2160 2160 18
        Draw-AppIcon $canvas.Graphics 130 130 1900
        $canvas.Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $canvas.Graphics.Dispose()
        $canvas.Bitmap.Dispose()
    }
}

function Save-PosterArt {
    param([string]$Path)

    $canvas = New-StoreCanvas 1440 2160 '#0d485c' '#176076'
    try {
        Add-SoftPattern $canvas.Graphics 1440 2160 22
        Draw-AppIcon $canvas.Graphics 180 220 1080

        $goldBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#ecb84a'))
        $creamBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#f6fcfc'))
        $accentPath = New-RoundedRectanglePath 520 1378 400 16 8
        $canvas.Graphics.FillPath($goldBrush, $accentPath)

        $titleFont = [System.Drawing.Font]::new('Segoe UI Semibold', 150, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
        $format = [System.Drawing.StringFormat]::new()
        $format.Alignment = [System.Drawing.StringAlignment]::Center
        $format.LineAlignment = [System.Drawing.StringAlignment]::Near
        $titleRect = [System.Drawing.RectangleF]::new(120, 1450, 1200, 430)
        $canvas.Graphics.DrawString("Domestic Financial`nPlanning", $titleFont, $creamBrush, $titleRect, $format)

        $canvas.Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)

        $format.Dispose()
        $titleFont.Dispose()
        $accentPath.Dispose()
        $goldBrush.Dispose()
        $creamBrush.Dispose()
    }
    finally {
        $canvas.Graphics.Dispose()
        $canvas.Bitmap.Dispose()
    }
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
Save-BoxArt $boxPath
Save-PosterArt $posterPath

Write-Output $boxPath
Write-Output $posterPath
