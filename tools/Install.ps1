[CmdletBinding()]
param([string]$SimHubPath = 'C:\Program Files (x86)\SimHub')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskInstallRoot=(Resolve-Path -LiteralPath $SimHubPath).Path
if(-not(Test-Path -LiteralPath (Join-Path $taskInstallRoot 'SimHubWPF.exe'))) {throw 'The destination does not contain SimHubWPF.exe.'}
if(@(Get-Process -Name SimHubWPF -ErrorAction SilentlyContinue).Count -gt 0){throw 'Close SimHub and run the installer again.'}
$taskSource=Join-Path $taskRoot 'artifacts\User.SimagicBridge.dll'
if(-not(Test-Path -LiteralPath $taskSource)){throw 'Build the plugin first using tools/Build.ps1.'}
$taskTarget=Join-Path $taskInstallRoot 'User.SimagicBridge.dll'
if(Test-Path -LiteralPath $taskTarget){
    $taskBackup=Join-Path $taskRoot 'diagnostics\private\installed-plugin-backups'
    New-Item -ItemType Directory -Path $taskBackup -Force|Out-Null
    Copy-Item -LiteralPath $taskTarget -Destination (Join-Path $taskBackup ('User.SimagicBridge-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'.dll'))
}
Copy-Item -LiteralPath $taskSource -Destination $taskTarget -Force
$taskSourceHash=(Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash
$taskTargetHash=(Get-FileHash -LiteralPath $taskTarget -Algorithm SHA256).Hash
if($taskSourceHash -ne $taskTargetHash){throw 'The installed DLL does not match the build.'}
Write-Host "Installed: $taskTarget"
Write-Host 'Start SimHub. Enable Simagic Bridge and show it in the main menu.'
