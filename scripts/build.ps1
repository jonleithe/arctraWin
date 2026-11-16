param([ValidateSet('All', 'Debug', 'Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$configurations = if ($Configuration -eq 'All') { @('Debug', 'Release') } elseif ($Configuration -eq 'Release') { @('Release') } else { @('Debug') }
Push-Location (Join-Path $repoRoot 'src/ArctraWin.Globe')
try {
    & npm.cmd ci
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' }
    & npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'Globe build failed' }
} finally { Pop-Location }
foreach ($buildConfiguration in $configurations) {
    Write-Host "Building ArctraWin ($buildConfiguration)..."
    & (Join-Path $PSScriptRoot 'build-backend.ps1') -Configuration $buildConfiguration
    & dotnet build (Join-Path $repoRoot 'src/ArctraWin.App/ArctraWin.App.csproj') -c $buildConfiguration -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw "Native app build failed ($buildConfiguration)" }
}
