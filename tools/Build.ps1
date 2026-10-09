[CmdletBinding()]
param([string]$SimHubPath = 'C:\Program Files (x86)\SimHub')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskFramework = Split-Path -Parent $taskCompiler
$taskWpf = Join-Path $taskFramework 'WPF'
$taskOutput = Join-Path $taskRoot 'artifacts'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskReferences = @('SimHub.Plugins.dll','SimHub.Logging.dll','GameReaderCommon.dll','Newtonsoft.Json.dll','BA63Driver.dll','SerialDash.dll','WoteverCommon.dll','log4net.dll') | ForEach-Object {
    $taskDll = Join-Path $SimHubPath $_
    if(-not(Test-Path -LiteralPath $taskDll)) { throw "Missing SimHub dependency: $taskDll" }
    '/reference:' + $taskDll
}
$taskReferences += @('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path $taskWpf $_) }
$taskReferences += @('System.Net.Http.dll','System.Drawing.dll','System.Xaml.dll','System.Core.dll') | ForEach-Object { '/reference:' + (Join-Path $taskFramework $_) }
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
& $taskCompiler /nologo /target:library /optimize+ /platform:anycpu ('/out:' + (Join-Path $taskOutput 'User.SimagicBridge.dll')) @taskReferences @taskSources
if($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
Write-Host 'Plugin built: artifacts/User.SimagicBridge.dll'
