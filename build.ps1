param([string]$OutputPath = "$PSScriptRoot\DesktopHover.exe")
$ErrorActionPreference = 'Stop'
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$argsForCompiler = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001',
    "/out:$OutputPath",
    "/win32icon:$PSScriptRoot\DesktopHover.ico",
    "/resource:$PSScriptRoot\DesktopHover.ico,DesktopHover.ico",
    '/r:System.dll', '/r:System.Core.dll', '/r:Microsoft.CSharp.dll',
    '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xaml.dll',
    "/r:$framework\WPF\WindowsBase.dll",
    "/r:$framework\WPF\PresentationCore.dll",
    "/r:$framework\WPF\UIAutomationClient.dll",
    "/r:$framework\WPF\UIAutomationTypes.dll",
    "$PSScriptRoot\Program.cs", "$PSScriptRoot\ShellResolver.cs", "$PSScriptRoot\PreviewWindow.cs",
    "$PSScriptRoot\AppSettings.cs", "$PSScriptRoot\Brand.cs", "$PSScriptRoot\SettingsWindow.cs",
    "$PSScriptRoot\Localization.cs"
)
& "$framework\csc.exe" @argsForCompiler
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
