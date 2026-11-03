param([switch]$OpenSettings)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceExe = Join-Path $projectRoot 'DesktopHover.exe'
$installFolder = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DesktopHover'
$installedExe = Join-Path $installFolder 'DesktopHover.exe'
$wasInstalled = Test-Path -LiteralPath $installedExe
$running = @(Get-Process DesktopHover -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $sourceExe -or $_.Path -eq $installedExe } |
    ForEach-Object { $_ })
if ($running.Count -gt 0) {
    try { $exitRequest = [Threading.EventWaitHandle]::OpenExisting('Local\DesktopHover_Exit') }
    catch { throw 'Quit the running Desktop Hover from its tray menu, then run Install.ps1 again. This older version does not support a safe update request.' }
    try { [void]$exitRequest.Set() } finally { $exitRequest.Dispose() }
    $deadline = [DateTime]::UtcNow.AddSeconds(70)
    do {
        $remaining = @(Get-Process DesktopHover -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $sourceExe -or $_.Path -eq $installedExe })
        if ($remaining.Count -eq 0) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($remaining.Count -gt 0) { throw 'Desktop Hover is still finishing its work. No files were replaced; try the installation again after it closes.' }
}
New-Item -ItemType Directory -Path $installFolder -Force | Out-Null
$updateExe = Join-Path $installFolder ('update-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    Copy-Item -LiteralPath $sourceExe -Destination $updateExe
    if (Test-Path -LiteralPath $installedExe) { [IO.File]::Replace($updateExe, $installedExe, [NullString]::Value) }
    else { [IO.File]::Move($updateExe, $installedExe) }
} finally { if (Test-Path -LiteralPath $updateExe) { Remove-Item -LiteralPath $updateExe } }
foreach ($documentation in @('LICENSE', 'THIRD-PARTY-NOTICES.md', 'README.md', 'README.lv.md', 'assets', 'docs')) {
    $documentationSource = Join-Path $projectRoot $documentation
    if (Test-Path -LiteralPath $documentationSource) {
        Copy-Item -LiteralPath $documentationSource -Destination $installFolder -Recurse -Force
    }
}
if (-not $wasInstalled) { Start-Process -FilePath $installedExe -ArgumentList '--enable-startup' -WindowStyle Hidden -Wait }
if ($OpenSettings) { Start-Process -FilePath $installedExe -ArgumentList '--settings' -WindowStyle Hidden }
else { Start-Process -FilePath $installedExe -WindowStyle Hidden }
Write-Output "Desktop Hover installed: $installedExe"
