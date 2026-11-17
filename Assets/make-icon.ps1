# Regenerates Assets/app.ico (multi-resolution) and optional preview PNGs.
# Usage: powershell -ExecutionPolicy Bypass -File Assets\make-icon.ps1 [-Preview]
param([switch]$Preview)
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$sizes = 16, 24, 32, 48, 64, 128, 256

function RR([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}
function Col([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }

# Draws the icon on a 256-unit canvas scaled to $px pixels.
function Render([int]$px, [int]$ss) {
    $big = $px * $ss
    $bmp = New-Object System.Drawing.Bitmap $big, $big, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.Clear([System.Drawing.Color]::Transparent)
    $k = $big / 256.0
    $g.ScaleTransform($k, $k)
    $small = $px -le 32
    $tiny = $px -le 24

    # Badge
    $badge = RR 6 6 244 244 54
    $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush ((New-Object System.Drawing.PointF 0, 6), (New-Object System.Drawing.PointF 0, 250), (Col '#1F3150'), (Col '#0B1424'))
    $g.FillPath($br, $badge)

    $white = New-Object System.Drawing.SolidBrush (Col '#FFFFFF')
    $paper = New-Object System.Drawing.SolidBrush (Col '#CFE2FF')
    $ink = New-Object System.Drawing.SolidBrush (Col '#1F3150')

    # Paper sheet going in (top) and out (bottom)
    $g.FillPath($paper, (RR 62 30 104 90 8))
    $g.FillPath($paper, (RR 62 150 104 78 8))
    # Printer body
    $g.FillPath($white, (RR 24 92 172 84 22))
    # Output slot
    $g.FillPath($ink, (RR 52 128 116 12 6))
    if (-not $small) {
        # text lines on printed receipt
        $line = New-Object System.Drawing.SolidBrush (Col '#7FA6DD')
        foreach ($y in 168, 186, 204) {
            $w = 72; if ($y -eq 204) { $w = 44 }
            $g.FillPath($line, (RR 78 ($y + 12) $w 7 3.5))
        }
        foreach ($y in 44, 62) { $g.FillPath($line, (RR 78 $y 72 7 3.5)) }
        $g.FillEllipse($ink, 168, 108, 14, 14)
    }
    elseif (-not $tiny) {
        $g.FillEllipse($ink, 168, 106, 16, 16)
    }

    # Bluetooth corner badge
    $cx = 186.0; $cy = 186.0; $r = 56.0
    $ring = New-Object System.Drawing.SolidBrush (Col '#0B1424')
    $g.FillEllipse($ring, $cx - $r - 8, $cy - $r - 8, 2 * ($r + 8), 2 * ($r + 8))
    $blue = New-Object System.Drawing.Drawing2D.LinearGradientBrush ((New-Object System.Drawing.PointF 0, ($cy - $r)), (New-Object System.Drawing.PointF 0, ($cy + $r)), (Col '#3AA0FF'), (Col '#0A74F0'))
    $g.FillEllipse($blue, $cx - $r, $cy - $r, 2 * $r, 2 * $r)
    $bw = 40.0; $bh = 68.0; $ox = $cx - $bw / 2; $oy = $cy - $bh / 2
    $pts = @(@(0.0, 0.28), @(1.0, 0.72), @(0.5, 1.0), @(0.5, 0.0), @(1.0, 0.28), @(0.0, 0.72)) |
        ForEach-Object { New-Object System.Drawing.PointF ([single]($ox + $_[0] * $bw)), ([single]($oy + $_[1] * $bh)) }
    $pw = 9.0; if ($small) { $pw = 12.0 }; if ($tiny) { $pw = 15.0 }
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([single]$pw)
    $pen.LineJoin = 'Round'; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawLines($pen, [System.Drawing.PointF[]]$pts)
    $g.Dispose()

    if ($ss -eq 1) { return $bmp }
    $out = New-Object System.Drawing.Bitmap $px, $px, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [System.Drawing.Graphics]::FromImage($out)
    $g2.InterpolationMode = 'HighQualityBicubic'; $g2.PixelOffsetMode = 'HighQuality'; $g2.CompositingMode = 'SourceCopy'
    $g2.DrawImage($bmp, 0, 0, $px, $px); $g2.Dispose(); $bmp.Dispose()
    return $out
}

function PngBytes($bmp) { $ms = New-Object System.IO.MemoryStream; $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); return ,$ms.ToArray() }

function DibBytes($bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $data = $bmp.LockBits($rect, 'ReadOnly', [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $raw = New-Object byte[] ($w * $h * 4)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $raw, 0, $raw.Length)
    $bmp.UnlockBits($data)
    $maskStride = [int]([math]::Ceiling($w / 32.0) * 4)
    $ms = New-Object System.IO.MemoryStream; $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([int]40); $bw.Write([int]$w); $bw.Write([int]($h * 2)); $bw.Write([int16]1); $bw.Write([int16]32)
    $bw.Write([int]0); $bw.Write([int]($w * $h * 4 + $maskStride * $h)); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
    for ($y = $h - 1; $y -ge 0; $y--) { $bw.Write($raw, $y * $w * 4, $w * 4) }
    $mask = New-Object byte[] ($maskStride * $h)
    for ($y = 0; $y -lt $h; $y++) {
        $sy = $h - 1 - $y
        for ($x = 0; $x -lt $w; $x++) {
            if ($raw[($sy * $w + $x) * 4 + 3] -eq 0) { $mask[$y * $maskStride + ($x -shr 3)] = $mask[$y * $maskStride + ($x -shr 3)] -bor (0x80 -shr ($x -band 7)) }
        }
    }
    $bw.Write($mask); $bw.Flush(); return ,$ms.ToArray()
}

$frames = @()
foreach ($s in $sizes) {
    $bmp = Render $s 8
    if ($Preview) { $bmp.Save((Join-Path $here "preview-$s.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
    if ($s -ge 64) { $bytes = PngBytes $bmp } else { $bytes = DibBytes $bmp }
    $frames += , @($s, $bytes); $bmp.Dispose()
}

$ms = New-Object System.IO.MemoryStream; $w = New-Object System.IO.BinaryWriter $ms
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $s = $f[0]; $b = $f[1]
    $dim = $s; if ($s -ge 256) { $dim = 0 }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$b.Length); $w.Write([int]$offset)
    $offset += $b.Length
}
foreach ($f in $frames) { $w.Write($f[1]) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $here 'app.ico'), $ms.ToArray())
Write-Host "Wrote app.ico ($($ms.Length) bytes)"
