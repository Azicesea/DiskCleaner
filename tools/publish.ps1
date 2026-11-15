# 打包成自包含单文件 exe（目标机器不需要装 .NET）
# 用法：powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'publish'

Get-Process DiskCleaner.App -ErrorAction SilentlyContinue | Stop-Process -Force
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish (Join-Path $root 'src\DiskCleaner.App\DiskCleaner.App.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $out

if ($LASTEXITCODE -ne 0) { throw "publish failed: $LASTEXITCODE" }

$exe = Join-Path $out 'DiskCleaner.App.exe'
if (-not (Test-Path $exe)) { throw "exe not found: $exe" }
$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Output "publish -> $exe ($size MB)"
$others = Get-ChildItem $out -File | Where-Object { $_.Name -ne 'DiskCleaner.App.exe' }
Write-Output ("other files: " + $others.Count)
foreach ($f in $others) { Write-Output ("  " + $f.Name + "  " + [math]::Round($f.Length / 1KB, 1) + " KB") }
