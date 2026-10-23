param([switch]$OpenSettings)
$ErrorActionPreference = 'Stop'
$sourceExe = Join-Path $PSScriptRoot 'DesktopHover.exe'
$installFolder = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DesktopHover'
$installedExe = Join-Path $installFolder 'DesktopHover.exe'
$wasInstalled = Test-Path -LiteralPath $installedExe
Get-Process DesktopHover -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $sourceExe -or $_.Path -eq $installedExe } |
    ForEach-Object { Stop-Process -Id $_.Id; if (-not $_.WaitForExit(5000)) { throw 'Desktop Hover did not stop.' } }
New-Item -ItemType Directory -Path $installFolder -Force | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $installedExe -Force
foreach ($documentation in @('README.md', 'README.lv.md', 'icon-preview.png', 'docs')) {
    $documentationSource = Join-Path $PSScriptRoot $documentation
    if (Test-Path -LiteralPath $documentationSource) {
        Copy-Item -LiteralPath $documentationSource -Destination $installFolder -Recurse -Force
    }
}
if (-not $wasInstalled) { Start-Process -FilePath $installedExe -ArgumentList '--enable-startup' -WindowStyle Hidden -Wait }
if ($OpenSettings) { Start-Process -FilePath $installedExe -ArgumentList '--settings' -WindowStyle Hidden }
else { Start-Process -FilePath $installedExe -WindowStyle Hidden }
Write-Output "Desktop Hover installed: $installedExe"
