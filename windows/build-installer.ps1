$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$output = Join-Path $workspace 'dist\DesktopEcosphere-v0.6'
& $compiler /nologo /target:winexe /platform:x64 /define:UNINSTALL "/out:$output\Uninstall.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "$PSScriptRoot\Installer.cs"
if($LASTEXITCODE -ne 0) { throw '卸载器编译失败' }
& "$PSScriptRoot\package.ps1"
$archive = Join-Path $workspace 'dist\DesktopEcosphere-Windows-x64-v0.6.zip'
$setup = Join-Path $workspace 'dist\DesktopEcosphere-Setup-v0.6.exe'
& $compiler /nologo /target:winexe /platform:x64 "/win32manifest:$PSScriptRoot\app.manifest" "/out:$setup" "/resource:$archive,payload.zip" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "$PSScriptRoot\Installer.cs"
if($LASTEXITCODE -ne 0) { throw '安装器编译失败' }
Write-Host "安装包：$setup"
