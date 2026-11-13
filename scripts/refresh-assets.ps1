param([string]$PythonPath = 'python', [switch]$TrayOnly, [switch]$OfficeOnly, [switch]$SettingsOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot '.verification\readme-assets'
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$metadata = Join-Path $env:WINDIR 'System32\WinMetadata'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$renderer = Join-Path $outputDirectory 'DocumentationAssets.exe'
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -Recurse -File | Sort-Object FullName | ForEach-Object FullName)
$compilerArgs = @(
    '/nologo', '/target:exe', '/main:DesktopHover.DocumentationAssets', '/platform:x64', '/optimize+', '/codepage:65001',
    "/out:$renderer", "/resource:$projectRoot\assets\DesktopHover.ico,DesktopHover.ico",
    '/r:System.dll', '/r:System.Core.dll', '/r:Microsoft.CSharp.dll', '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll', '/r:System.Xaml.dll', '/r:System.Xml.dll', '/r:System.IO.Compression.dll',
    "/r:$framework\WPF\WindowsBase.dll", "/r:$framework\WPF\PresentationCore.dll",
    "/r:$framework\WPF\UIAutomationClient.dll", "/r:$framework\WPF\UIAutomationTypes.dll",
    "/r:$framework\System.Runtime.dll", "/r:$framework\System.Runtime.WindowsRuntime.dll",
    "/r:$metadata\Windows.UI.winmd",
    "/r:$framework\System.Runtime.InteropServices.WindowsRuntime.dll",
    "/r:$metadata\Windows.Data.winmd", "/r:$metadata\Windows.Foundation.winmd", "/r:$metadata\Windows.Storage.winmd"
) + $sourceFiles + (Join-Path $PSScriptRoot 'assets\DocumentationAssets.cs')
& "$framework\csc.exe" @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Asset renderer compilation failed.' }
if ($SettingsOnly) {
    & $renderer $projectRoot settings
    if ($LASTEXITCODE -ne 0) { throw 'Settings screenshot rendering failed.' }
    return
}
if ($OfficeOnly) {
    & $renderer $projectRoot office
    if ($LASTEXITCODE -ne 0) { throw 'Office screenshot rendering failed. Generate the demonstration fixtures with a full refresh first.' }
    return
}
if ($TrayOnly) {
    & $renderer $projectRoot tray
    if ($LASTEXITCODE -ne 0) { throw 'Tray screenshot rendering failed.' }
    return
}
& $renderer $projectRoot artwork
if ($LASTEXITCODE -ne 0) { throw 'Artwork generation failed.' }
& $PythonPath (Join-Path $PSScriptRoot 'assets\make-samples.py') $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Office fixture generation failed. Python needs python-docx, openpyxl and python-pptx.' }
# Embed the newly generated icon for the screenshot run.
& "$framework\csc.exe" @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Asset renderer compilation failed.' }
& $renderer $projectRoot screenshots
if ($LASTEXITCODE -ne 0) { throw 'Screenshot rendering failed.' }
Write-Output 'Updated application icon, README logo and all 11 documentation screenshots.'
