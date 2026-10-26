param([string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'DesktopHover.exe'))
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot 'src'
$iconPath = Join-Path $projectRoot 'assets\DesktopHover.ico'
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$windowsMetadata = Join-Path $env:WINDIR 'System32\WinMetadata'
$pdfReferences = @(
    (Join-Path $framework 'System.Runtime.dll'),
    (Join-Path $framework 'System.Runtime.WindowsRuntime.dll'),
    (Join-Path $windowsMetadata 'Windows.Data.winmd'),
    (Join-Path $windowsMetadata 'Windows.Foundation.winmd'),
    (Join-Path $windowsMetadata 'Windows.Storage.winmd')
)
foreach ($pdfReference in $pdfReferences) {
    if (-not (Test-Path -LiteralPath $pdfReference -PathType Leaf)) {
        throw "Missing Windows PDF build reference: $pdfReference. Build on Windows 10/11 with .NET Framework 4.8 installed."
    }
}
$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -Recurse -File |
    Sort-Object FullName | ForEach-Object FullName)
if ($sourceFiles.Count -eq 0) { throw "No C# source files found in $sourceRoot" }
New-Item -ItemType Directory -Path (Split-Path -Parent $resolvedOutput) -Force | Out-Null
$argsForCompiler = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001',
    "/out:$resolvedOutput",
    "/win32icon:$iconPath",
    "/resource:$iconPath,DesktopHover.ico",
    '/r:System.dll', '/r:System.Core.dll', '/r:Microsoft.CSharp.dll',
    '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xaml.dll',
    '/r:System.Xml.dll', '/r:System.IO.Compression.dll',
    "/r:$framework\WPF\WindowsBase.dll",
    "/r:$framework\WPF\PresentationCore.dll",
    "/r:$framework\WPF\UIAutomationClient.dll",
    "/r:$framework\WPF\UIAutomationTypes.dll"
) + @($pdfReferences | ForEach-Object { "/r:$_" }) + $sourceFiles
& "$framework\csc.exe" @argsForCompiler
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Write-Output "Built Desktop Hover: $resolvedOutput"
