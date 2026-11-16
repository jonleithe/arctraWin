param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
$repoRoot = Split-Path $PSScriptRoot -Parent
$appPath = Join-Path $repoRoot "src/ArctraWin.App/bin/x64/$Configuration/net11.0-windows10.0.26100.0/win-x64/ArctraWin.App.exe"
if (!(Test-Path -LiteralPath $appPath)) { throw 'Application not built. Run scripts/build.ps1 first.' }
Start-Process -FilePath $appPath -WindowStyle Normal
