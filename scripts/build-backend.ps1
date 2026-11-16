param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$cmakeCommand = Get-Command cmake -ErrorAction SilentlyContinue
if ($cmakeCommand) { $cmakeExe = $cmakeCommand.Source }
else {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $vsRoot = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    $cmakeExe = Join-Path $vsRoot 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
}
if (!(Test-Path $cmakeExe)) { throw 'Install Visual Studio C++ tools and CMake.' }
$buildDirectory = Join-Path $repoRoot 'build/backend'
& $cmakeExe -S (Join-Path $repoRoot 'src/ArctraWin.Backend') -B $buildDirectory -A x64
if ($LASTEXITCODE -ne 0) { throw 'Backend configuration failed' }
& $cmakeExe --build $buildDirectory --config $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Backend build failed' }
$ctestExe = Join-Path (Split-Path $cmakeExe) 'ctest.exe'
& $ctestExe --test-dir $buildDirectory -C $Configuration --output-on-failure
if ($LASTEXITCODE -ne 0) { throw 'Backend checks failed' }
