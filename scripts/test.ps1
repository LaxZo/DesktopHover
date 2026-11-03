param([switch]$BuildOnly)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$windowsMetadata = Join-Path $env:WINDIR 'System32\WinMetadata'
New-Item -ItemType Directory -Path (Join-Path $workspace '.verification') -Force | Out-Null
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $workspace 'src') -Filter '*.cs' -Recurse -File | Sort-Object FullName | ForEach-Object FullName)
$argsForCompiler = @(
    '/nologo', '/target:exe', '/main:DesktopHover.Regression', '/define:REGRESSION', '/platform:x64', '/optimize+', '/codepage:65001',
    "/out:$workspace\.verification\Regression.exe",
    "/resource:$workspace\assets\DesktopHover.ico,DesktopHover.ico",
    '/r:System.dll', '/r:System.Core.dll', '/r:Microsoft.CSharp.dll',
    '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xaml.dll',
    '/r:System.Xml.dll', '/r:System.IO.Compression.dll',
    "/r:$framework\WPF\WindowsBase.dll", "/r:$framework\WPF\PresentationCore.dll",
    "/r:$framework\WPF\UIAutomationClient.dll", "/r:$framework\WPF\UIAutomationTypes.dll",
    "/r:$framework\System.Runtime.dll", "/r:$framework\System.Runtime.WindowsRuntime.dll",
    "/r:$windowsMetadata\Windows.Data.winmd", "/r:$windowsMetadata\Windows.Foundation.winmd", "/r:$windowsMetadata\Windows.Storage.winmd"
) + $sourceFiles + (Join-Path $workspace 'tests\Regression.cs')
& "$framework\csc.exe" @argsForCompiler
if ($LASTEXITCODE -ne 0) { throw 'Regression harness compilation failed.' }
if ($BuildOnly) { return }
& "$workspace\.verification\Regression.exe" "$workspace\.verification\regression"
if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }

