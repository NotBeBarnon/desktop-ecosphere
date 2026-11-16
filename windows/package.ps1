$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot
$output = Join-Path $workspace 'dist\DesktopEcosphere-v0.10'
if (!(Test-Path "$output\DesktopEcosphere.exe")) { throw '请先运行 windows/build.ps1' }
Copy-Item "$workspace\README.md" "$output\使用说明.md" -Force
$files = @("$output\DesktopEcosphere.exe", "$output\Microsoft.Web.WebView2.Core.dll", "$output\Microsoft.Web.WebView2.WinForms.dll", "$output\WebView2Loader.dll", "$output\assets", "$output\LICENSE-upstream.txt", "$output\LICENSE-WebView2.txt", "$output\使用说明.md")
if(Test-Path "$output\Uninstall.exe") { $files += "$output\Uninstall.exe" }
$files += "$output\LICENSE.txt"
$archive = Join-Path $workspace 'dist\DesktopEcosphere-Windows-x64-v0.10.zip'
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
Write-Host "便携压缩包：$archive"
