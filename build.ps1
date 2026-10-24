param([string]$OutputPath = "$PSScriptRoot\DesktopHover.exe")
$ErrorActionPreference = 'Stop'
# Keep the familiar root command; the implementation lives with the other scripts.
& (Join-Path $PSScriptRoot 'scripts\build.ps1') -OutputPath $OutputPath
