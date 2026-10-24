param([switch]$OpenSettings)
$ErrorActionPreference = 'Stop'
# Keep the familiar root command; the implementation lives with the other scripts.
& (Join-Path $PSScriptRoot 'scripts\Install.ps1') -OpenSettings:$OpenSettings
