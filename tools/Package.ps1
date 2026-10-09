[CmdletBinding()]
param([string]$SimHubPath = 'C:\Program Files (x86)\SimHub')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Build.ps1') -SimHubPath $SimHubPath
$taskDll = Join-Path $taskRoot 'artifacts\User.SimagicBridge.dll'
$taskVersion = (Get-Item -LiteralPath $taskDll).VersionInfo.FileVersion
$taskRelease = Join-Path $taskRoot 'release'
New-Item -ItemType Directory -Path $taskRelease -Force | Out-Null
$taskZip = Join-Path $taskRelease ('SimagicBridge-' + $taskVersion + '-English.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskStream = [IO.File]::Open($taskZip, [IO.FileMode]::Create)
$taskArchive = New-Object IO.Compression.ZipArchive($taskStream, [IO.Compression.ZipArchiveMode]::Create)
try {
    $taskFiles = @{
        'User.SimagicBridge.dll' = $taskDll
        'README.md' = (Join-Path $taskRoot 'README.md')
        'CHANGELOG.md' = (Join-Path $taskRoot 'CHANGELOG.md')
        'LICENSE' = (Join-Path $taskRoot 'LICENSE')
        'docs/TECHNICAL.md' = (Join-Path $taskRoot 'docs\TECHNICAL.md')
        'docs/VALIDATION.md' = (Join-Path $taskRoot 'docs\VALIDATION.md')
        'docs/settings-preview.png' = (Join-Path $taskRoot 'docs\settings-preview.png')
    }
    foreach ($taskName in ($taskFiles.Keys | Sort-Object)) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskArchive, $taskFiles[$taskName], $taskName) | Out-Null
    }
} finally {
    $taskArchive.Dispose()
    $taskStream.Dispose()
}
$taskHashes = @($taskDll, $taskZip) | ForEach-Object {
    $taskHash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    $taskHash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_)
}
$taskHashes | Set-Content -LiteralPath (Join-Path $taskRelease 'SHA256SUMS.txt') -Encoding ASCII
Write-Host "Release package: $taskZip"
