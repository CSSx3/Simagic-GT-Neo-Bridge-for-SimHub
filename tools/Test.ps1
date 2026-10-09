[CmdletBinding()]
param([string]$SimHubPath = 'C:\Program Files (x86)\SimHub',[switch]$Live,[switch]$Preview)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Build.ps1') -SimHubPath $SimHubPath
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskFramework=Split-Path -Parent $taskCompiler
$taskSimHub=$SimHubPath
$taskReferences=@('Newtonsoft.Json.dll','SimHub.Plugins.dll','BA63Driver.dll','SerialDash.dll','WoteverCommon.dll','GongSolutions.WPF.DragDrop.dll')|ForEach-Object{'/reference:'+(Join-Path $taskSimHub $_)}
$taskReferences+=@('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll')|ForEach-Object{'/reference:'+(Join-Path $taskFramework ('WPF\'+$_))}
$taskReferences+=@('System.Net.Http.dll','System.Drawing.dll','System.Xaml.dll')|ForEach-Object{'/reference:'+(Join-Path $taskFramework $_)}
$taskReferences+='/reference:'+(Join-Path $taskRoot 'artifacts\User.SimagicBridge.dll')
& $taskCompiler /nologo /target:exe ('/out:'+(Join-Path $taskRoot 'artifacts\BridgeTests.exe')) @taskReferences (Join-Path $taskRoot 'tests\BridgeTests.cs')
if($LASTEXITCODE -ne 0){throw 'Test build failed.'}
$taskMode=@();if($Live){$taskMode+='--live'};if($Preview){$taskMode+='--preview'}
& (Join-Path $taskRoot 'artifacts\BridgeTests.exe') $taskRoot $taskSimHub @taskMode
if($LASTEXITCODE -ne 0){throw 'Plugin verification failed.'}
