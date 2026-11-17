param(
    [string]$OutName = "",   # 可选：指定产物文件名（默认 Admin=dist\Rx工具箱.exe，Player=dist\player\Rx工具箱.exe）
    [ValidateSet("Admin", "Player")]
    [string]$Edition = "Admin"
)
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$isAdmin = ($Edition -eq "Admin")

$verFile = Join-Path $root "VERSION"
if (-not (Test-Path $verFile)) { throw "找不到 VERSION" }
$verFull = (Get-Content $verFile -Raw).Trim()
$verShort = ($verFull -split '\.')[0..1] -join '.'

$csc = $null
foreach ($c in @(
        "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe")) {
    if (Test-Path $c) { $csc = $c; break }
}
if (-not $csc) { throw "未找到 csc.exe" }

$adminOnlyFiles = @("MainForm.Data.cs", "MainForm.DataBackup.cs", "MainForm.DevHub.cs")
$srcFiles = Get-ChildItem (Join-Path $root "src") -Filter *.cs | Where-Object {
    $isAdmin -or ($adminOnlyFiles -notcontains $_.Name)
} | Sort-Object Name | ForEach-Object { $_.FullName }
if (-not $srcFiles -or $srcFiles.Count -eq 0) { throw "未找到 src\*.cs 源码文件" }
$prog = Join-Path $root "build\program.cs"
$icon = Join-Path $root "rx.ico"

# 数据改成运行时读 phrases.json，不再需要 data.cs

$dist = Join-Path $root "dist"
$outDir = if ($isAdmin) { $dist } else { Join-Path $dist "player" }
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
Get-ChildItem $outDir -Filter *.exe -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

$args0 = @(
    "/nologo", "/target:winexe", "/platform:x64", "/optimize+", "/utf8output",
    "/r:System.dll", "/r:System.Core.dll", "/r:System.Drawing.dll", "/r:System.Windows.Forms.dll"
)
if (Test-Path $icon) { $args0 += "/win32icon:$icon" }

# 使用指南里的两张示意图（仅管理版嵌进 exe；公开玩家版不含服务器列表与 IP 截图）
if ($isAdmin) {
    $imgSrv = Join-Path $root "img\guide_srv.jpg"
    $imgIp = Join-Path $root "img\guide_ip.png"
    if (Test-Path $imgSrv) { $args0 += "/resource:$imgSrv,guide_srv.jpg" }
    if (Test-Path $imgIp) { $args0 += "/resource:$imgIp,guide_ip.png" }
}

# Windows OCR（0 体积，已移除废弃的 UIAutomation WPF 依赖）
$winmd = "C:\Windows\System32\WinMetadata"
$wrGac = "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Runtime.WindowsRuntime\v4.0_4.0.0.0__b77a5c561934e089\System.Runtime.WindowsRuntime.dll"
if (Test-Path $winmd) {
    foreach ($m in @("Windows.Media.winmd", "Windows.Foundation.winmd", "Windows.Graphics.winmd", "Windows.Storage.winmd", "Windows.Globalization.winmd")) {
        $f = Join-Path $winmd $m
        if (Test-Path $f) { $args0 += "/r:" + $f }
    }
    $coreRt = Join-Path (Split-Path $csc -Parent) "System.Runtime.dll"
    if (Test-Path $coreRt) { $args0 += "/r:" + $coreRt }
    if (Test-Path $wrGac) { $args0 += "/r:" + $wrGac }
}

if ($OutName -and $OutName.Trim().Length -gt 0) {
    $out = Join-Path $outDir $OutName
} else {
    $out = Join-Path $outDir "Rx工具箱.exe"
}
$defineArg = if ($isAdmin) { "/define:ADMIN_EDITION" } else { "/define:PLAYER_EDITION" }
& $csc ($args0 + @($defineArg, "/out:$out", $prog) + $srcFiles)
if ($LASTEXITCODE -ne 0) { throw "编译失败 (exit $LASTEXITCODE)" }

Write-Output ""
Write-Output ("  产物（" + $Edition + "）：")
Get-ChildItem $outDir -Filter *.exe | ForEach-Object {
    "    {0}  {1:N0} KB" -f $_.Name, ($_.Length / 1KB)
}
