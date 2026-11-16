$ErrorActionPreference = 'Stop'
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$output = Join-Path (Split-Path $PSScriptRoot) 'dist\UpdateProbe.exe'
& $compiler /nologo /target:exe "/out:$output" /r:System.Net.Http.dll /r:System.Web.Extensions.dll "$PSScriptRoot\AppInfo.cs" "$PSScriptRoot\UpdateChecker.cs" "$PSScriptRoot\UpdateProbe.cs"
if($LASTEXITCODE -ne 0) { throw '网络更新探针编译失败' }
& $output
if($LASTEXITCODE -ne 0) { throw '真实网络查询不可用；不会被视为已是最新版本' }
