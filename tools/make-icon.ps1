Add-Type -AssemblyName System.Drawing

$srcPath = 'E:\DSH\Clean\src\DiskCleaner.App\Assets\app-src.png'
$outPng  = 'E:\DSH\Clean\src\DiskCleaner.App\Assets\app.png'
$outIco  = 'E:\DSH\Clean\src\DiskCleaner.App\Assets\app.ico'

$src = [System.Drawing.Image]::FromFile($srcPath)

function Resize-ToBitmap([System.Drawing.Image]$image, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($image, 0, 0, $size, $size)
    $g.Dispose()
    return $bmp
}

function Make-WhiteTransparent([System.Drawing.Bitmap]$bmp) {
    $rect = New-Object System.Drawing.Rectangle 0, 0, $bmp.Width, $bmp.Height
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $len = $data.Stride * $bmp.Height
    $bytes = New-Object byte[] $len
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $len)
    for ($i = 0; $i -lt $len; $i += 4) {
        $a = $bytes[$i + 3]
        if ($a -eq 0) { continue }
        $b = $bytes[$i]; $g = $bytes[$i + 1]; $r = $bytes[$i + 2]
        $min = [Math]::Min($r, [Math]::Min($g, $b))
        if ($min -le 235) { continue }
        $t = ($min - 235) / 20.0
        if ($t -gt 1) { $t = 1 }
        $bytes[$i + 3] = [byte]($a * (1.0 - $t))
    }
    [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $len)
    $bmp.UnlockBits($data)
}

function To-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $arr = $ms.ToArray()
    $ms.Dispose()
    return , $arr
}

# 大图（界面里用）256
$big = Resize-ToBitmap $src 256
Make-WhiteTransparent $big
$big.Save($outPng, [System.Drawing.Imaging.ImageFormat]::Png)
$big.Dispose()
Write-Output "png -> $outPng"

# 多尺寸 ICO
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $bmp = Resize-ToBitmap $src $s
    Make-WhiteTransparent $bmp
    $pngs += , (To-PngBytes $bmp)
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create($outIco)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]0)
    $bw.Write([Byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$pngs[$i].Length)
    $bw.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush()
$bw.Close()
$fs.Close()
$src.Dispose()
Write-Output ("ico -> $outIco (" + (Get-Item $outIco).Length + " bytes)")
