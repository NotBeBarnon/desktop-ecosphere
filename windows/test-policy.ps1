$ErrorActionPreference = 'Stop'
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$output = Join-Path (Split-Path $PSScriptRoot) 'dist\PolicyTests.exe'
$package = Join-Path $PSScriptRoot 'packages\webview2\lib\net462'
& $compiler /nologo /target:exe /main:DesktopEcosphere.PolicyTests "/out:$output" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Web.Extensions.dll /r:System.Runtime.Serialization.dll "/r:$package\Microsoft.Web.WebView2.Core.dll" "/r:$package\Microsoft.Web.WebView2.WinForms.dll" "$PSScriptRoot\Program.cs" "$PSScriptRoot\WallpaperSurface.cs" "$PSScriptRoot\SettingsForm.cs" "$PSScriptRoot\EnergyPolicy.cs" "$PSScriptRoot\Interaction.cs" "$PSScriptRoot\PolicyTests.cs"
if($LASTEXITCODE -ne 0) { throw '策略测试编译失败' }
& $output
if($LASTEXITCODE -ne 0) { throw '策略测试失败' }
