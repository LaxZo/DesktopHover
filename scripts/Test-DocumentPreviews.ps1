param([string]$AppPath = '', [switch]$SkipBuild, [switch]$NativePowerPoint)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot '.verification\document-previews'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
if (-not $AppPath) { $AppPath = Join-Path $outputDirectory 'DesktopHover.exe' }
$AppPath = [IO.Path]::GetFullPath($AppPath)
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') -OutputPath $AppPath }
if (-not (Test-Path -LiteralPath $AppPath -PathType Leaf)) { throw "App not found: $AppPath" }
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testExe = Join-Path $outputDirectory 'DocumentPreviewTests.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tests') -Filter '*.cs' -File |
    Sort-Object FullName | ForEach-Object FullName)
$compilerArguments = @('/nologo', '/target:exe', '/platform:x64', '/codepage:65001',
    '/main:DocumentPreviewTests', "/out:$testExe", '/r:System.dll', '/r:System.Core.dll',
    '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.IO.Compression.dll', '/r:Microsoft.CSharp.dll') + $sources
& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
$testArguments = @($AppPath, $outputDirectory)
if ($NativePowerPoint) {
    $fixtureDirectory = Join-Path $projectRoot 'tests\fixtures'
    $testArguments += @((Join-Path $fixtureDirectory 'styled-native.pptx'), (Join-Path $fixtureDirectory 'styled-native-reference.png'))
}
& $testExe @testArguments
if ($LASTEXITCODE -ne 0) { throw 'Document preview regression tests failed.' }
