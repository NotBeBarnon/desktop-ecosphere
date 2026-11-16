$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$package = Join-Path $project 'packages\webview2'
if (!(Test-Path "$package\lib\net462\Microsoft.Web.WebView2.Core.dll")) {
    New-Item -ItemType Directory -Force "$project\packages" | Out-Null
    Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.2903.40/microsoft.web.webview2.1.0.2903.40.nupkg' -OutFile "$project\packages\webview2.zip"
    Expand-Archive "$project\packages\webview2.zip" $package -Force
}
$output = Join-Path (Split-Path $project) 'dist\DesktopEcosphere-v0.6'
New-Item -ItemType Directory -Force $output | Out-Null
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/win32manifest:$project\app.manifest" "/out:$output\DesktopEcosphere.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll /r:System.Core.dll /r:System.Web.Extensions.dll "/r:$package\lib\net462\Microsoft.Web.WebView2.Core.dll" "/r:$package\lib\net462\Microsoft.Web.WebView2.WinForms.dll" "$project\Program.cs" "$project\WallpaperSurface.cs" "$project\SettingsForm.cs" "$project\EnergyPolicy.cs" "$project\Interaction.cs"
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Copy-Item "$package\lib\net462\Microsoft.Web.WebView2.Core.dll","$package\lib\net462\Microsoft.Web.WebView2.WinForms.dll","$package\runtimes\win-x64\native\WebView2Loader.dll" $output -Force
$assets = Join-Path $output 'assets'
New-Item -ItemType Directory -Force $assets | Out-Null
foreach ($folder in @('scenes','vendor','ui')) { Copy-Item "$project\..\upstream\$folder" $assets -Recurse -Force }
Copy-Item "$project\..\upstream\LICENSE" "$output\LICENSE-upstream.txt" -Force
Copy-Item "$project\..\LICENSE" "$output\LICENSE.txt" -Force
Copy-Item "$package\LICENSE.txt" "$output\LICENSE-WebView2.txt" -Force
Write-Host "构建完成：$output\DesktopEcosphere.exe"
