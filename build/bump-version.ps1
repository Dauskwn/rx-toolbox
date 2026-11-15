# 统一改版本号：rx-core.cs 的 APP_VERSION + program.cs 的程序集版本 + VERSION
# 用法： pwsh -File build\bump-version.ps1 1.2
param([Parameter(Mandatory=$true)][string]$ver)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

if ($ver -notmatch '^\d+\.\d+$') { throw "版本号格式应为 1.2" }

$core = Join-Path $root "rx-core.cs"
$prog = Join-Path $root "build\program.cs"
$vf   = Join-Path $root "VERSION"

$c = [IO.File]::ReadAllText($core, [Text.Encoding]::UTF8)
$new = [regex]::Replace($c, 'APP_VERSION\s*=\s*"V[\d\.]+"', 'APP_VERSION = "V' + $ver + '"')
if ($new -eq $c -and $c -notmatch 'APP_VERSION\s*=\s*"V') { throw "rx-core.cs 里找不到 APP_VERSION" }
[IO.File]::WriteAllText($core, $new, (New-Object Text.UTF8Encoding $false))

$p = [IO.File]::ReadAllText($prog, [Text.Encoding]::UTF8)
$p = [regex]::Replace($p, 'AssemblyVersion\("[\d\.]+"\)',     'AssemblyVersion("' + $ver + '.0.0")')
$p = [regex]::Replace($p, 'AssemblyFileVersion\("[\d\.]+"\)', 'AssemblyFileVersion("' + $ver + '.0.0")')
[IO.File]::WriteAllText($prog, $p, (New-Object Text.UTF8Encoding $false))

[IO.File]::WriteAllText($vf, $ver + ".0`r`n", (New-Object Text.UTF8Encoding $false))

Write-Output ("  APP_VERSION = V" + $ver)
Write-Output ("  AssemblyVersion = " + $ver + ".0.0")
Write-Output ("  VERSION = " + $ver + ".0")
