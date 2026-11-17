Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot 'milkpad-setup.ico'
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$blobs = New-Object 'System.Collections.Generic.List[byte[]]'

function New-IconPng([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $scale = $size / 256.0
    $graphics.ScaleTransform($scale, $scale)

    $background = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 21, 21, 26))
    $orange = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 121, 26))
    $orangePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 121, 26), 5)
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 246, 246, 250))

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(8, 8, 48, 48, 180, 90)
    $path.AddArc(200, 8, 48, 48, 270, 90)
    $path.AddArc(200, 200, 48, 48, 0, 90)
    $path.AddArc(8, 200, 48, 48, 90, 90)
    $path.CloseFigure()
    $graphics.FillPath($background, $path)
    $graphics.DrawPath($orangePen, $path)

    $graphics.FillRectangle($white, 114, 55, 28, 77)
    $arrow = New-Object 'System.Drawing.Point[]' 3
    $arrow[0] = New-Object System.Drawing.Point(88, 124)
    $arrow[1] = New-Object System.Drawing.Point(168, 124)
    $arrow[2] = New-Object System.Drawing.Point(128, 177)
    $graphics.FillPolygon($white, $arrow)

    $graphics.FillRectangle($orange, 73, 190, 110, 17)
    $graphics.FillRectangle($background, 88, 194, 80, 9)

    $graphics.Dispose()
    $background.Dispose()
    $orange.Dispose()
    $orangePen.Dispose()
    $white.Dispose()
    $path.Dispose()

    $memory = New-Object System.IO.MemoryStream
    $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    $bytes = $memory.ToArray()
    $memory.Dispose()
    return $bytes
}

foreach ($size in $sizes) {
    $blobs.Add((New-IconPng $size))
}

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$blobs[$i].Length)
    $writer.Write([UInt32]$offset)
    $offset += $blobs[$i].Length
}
foreach ($blob in $blobs) { $writer.Write($blob) }
$writer.Flush()
[System.IO.File]::WriteAllBytes($out, $stream.ToArray())
$writer.Dispose()
$stream.Dispose()
Write-Host "Wrote $out"
