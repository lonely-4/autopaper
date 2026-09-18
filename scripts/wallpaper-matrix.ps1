<#
.SYNOPSIS
    实测哪些图片格式真的能设成 Windows 壁纸。

.DESCRIPTION
    用 System.Drawing 逐个生成真图（bmp / png / jpg / gif / tif），
    每次只放一张进壁纸目录，跑 autopaper apply，然后读回
    HKCU\Control Panel\Desktop\Wallpaper 确认到底改没改。

    这个脚本只在 Windows 上有意义。

.EXAMPLE
    dotnet build -c Release
    pwsh scripts/wallpaper-matrix.ps1
#>

param(
    [string]$Dll = 'src/AutoPaper/bin/Release/net10.0/autopaper.dll'
)

$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    Write-Host '不是 Windows，跳过（这个脚本只在 Windows 上有意义）。'
    exit 0
}

if (-not (Test-Path $Dll)) {
    Write-Host "找不到 $Dll，先跑 dotnet build -c Release" -ForegroundColor Red
    exit 1
}

Add-Type -AssemblyName System.Drawing

$root = Join-Path $env:RUNNER_TEMP 'autopaper-matrix'
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$wallpapers = Join-Path $root 'wallpapers'
$staging = Join-Path $root 'staging'
New-Item -ItemType Directory -Force -Path $wallpapers, $staging | Out-Null

# 每个候选格式造一张 1920x1080 的真图
$formats = @(
    @{ Ext = 'bmp'; Image = [System.Drawing.Imaging.ImageFormat]::Bmp }
    @{ Ext = 'png'; Image = [System.Drawing.Imaging.ImageFormat]::Png }
    @{ Ext = 'jpg'; Image = [System.Drawing.Imaging.ImageFormat]::Jpeg }
    @{ Ext = 'gif'; Image = [System.Drawing.Imaging.ImageFormat]::Gif }
    @{ Ext = 'tif'; Image = [System.Drawing.Imaging.ImageFormat]::Tiff }
)

foreach ($f in $formats) {
    $bitmap = New-Object System.Drawing.Bitmap 1920, 1080
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::FromArgb(32, 96, 160))
    $graphics.Dispose()
    $bitmap.Save((Join-Path $staging "1.$($f.Ext)"), $f.Image)
    $bitmap.Dispose()
}

# range: [1] 表示每天都用槽位 1，这样无论今天是星期几都会选中我们放的那张
$config = Join-Path $root 'config.yaml'
"range: [1]`nstyle: fill`n" | Set-Content -Path $config -Encoding UTF8

Write-Host "壁纸目录：$wallpapers"
Write-Host "配置文件：$config"
Write-Host ''

$results = @()

foreach ($f in $formats) {
    $fileName = "1.$($f.Ext)"

    # 每次只留一张图，确保选中的就是它
    Get-ChildItem $wallpapers -File | Remove-Item -Force
    $target = Join-Path $wallpapers $fileName
    Copy-Item (Join-Path $staging $fileName) $target

    $output = & dotnet $Dll apply --config $config 2>&1 | Out-String
    $exitCode = $LASTEXITCODE

    $registryValue = (Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper
    $registryLeaf = if ($registryValue) { Split-Path $registryValue -Leaf } else { '(空)' }

    $verdict = if ($exitCode -eq 0 -and $registryLeaf -eq $fileName) { 'OK' } else { '失败' }

    $results += [pscustomobject]@{
        格式    = $f.Ext
        退出码  = $exitCode
        注册表  = $registryLeaf
        判定    = $verdict
    }

    if ($verdict -ne 'OK') {
        Write-Host "--- $fileName 的输出 ---"
        Write-Host $output.Trim()
        Write-Host ''
    }
}

Write-Host ''
$results | Format-Table -AutoSize | Out-String | Write-Host

$failed = @($results | Where-Object { $_.判定 -ne 'OK' })
if ($failed.Count -gt 0) {
    Write-Host "这些格式没能设成壁纸：$(($failed.格式) -join '、')" -ForegroundColor Yellow
    exit 1
}

Write-Host '所有格式都成功设成了壁纸。' -ForegroundColor Green
exit 0
