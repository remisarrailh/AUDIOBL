# Generates a minimal 32x32 .ico file for AUDIOBL
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 32, 32
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Transparent)
$g.FillEllipse([System.Drawing.Brushes]::DodgerBlue, 4, 4, 24, 24)
$g.DrawString("A", (New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold)), [System.Drawing.Brushes]::White, 7, 6)
$g.Dispose()

$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

# ICO header + image
$pngBytes = $ms.ToArray()
$ms.Dispose()

$icoStream = New-Object System.IO.MemoryStream

# ICO header
$writer = New-Object System.IO.BinaryWriter($icoStream)
$writer.Write([uint16]0)    # Reserved
$writer.Write([uint16]1)    # Type = ICO
$writer.Write([uint16]1)    # Count = 1

# Image directory entry
$writer.Write([byte]32)     # Width
$writer.Write([byte]32)     # Height
$writer.Write([byte]0)      # ColorCount
$writer.Write([byte]0)      # Reserved
$writer.Write([uint16]1)    # Planes
$writer.Write([uint16]32)   # BitCount
$writer.Write([uint32]$pngBytes.Length)
$writer.Write([uint32]22)   # Offset to image data

$writer.Write($pngBytes)
$writer.Flush()

$outPath = Join-Path $PSScriptRoot "Resources\tray.ico"
[System.IO.File]::WriteAllBytes($outPath, $icoStream.ToArray())
$icoStream.Dispose()

Write-Host "Icon generated at: $outPath"
