param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'build-backend.ps1') -Configuration $Configuration
$firstSource = Get-Content (Join-Path $repoRoot 'assets/sample-data/cpf/source.json') -Raw | ConvertFrom-Json
$firstSource | Add-Member -NotePropertyName key -NotePropertyValue lageos1
$sources = @($firstSource)
$additionalSources = Get-Content (Join-Path $repoRoot 'assets/sample-data/cpf/additional-sources.json') -Raw | ConvertFrom-Json
foreach ($additional in $additionalSources) { $sources += $additional }
$catalog = Import-Csv (Join-Path $repoRoot 'assets/catalog/edc-active-satellites.csv')
$exporter = Join-Path $repoRoot "build/backend/$Configuration/arctrawin-export.exe"
foreach ($source in $sources) {
    $target = $catalog | Where-Object { $_.cpf_key -eq $source.key } | Select-Object -First 1
    if (!$target) { throw "Unknown target: $($source.key)" }
    & $exporter --cpf (Join-Path $repoRoot "assets/sample-data/cpf/$($source.filename)") `
    --output (Join-Path $repoRoot "assets/sample-data/$($source.key)-trajectory-v1.json") `
    --key $source.key --name $target.name --norad ([int]$target.norad_id) --ilrs ([int]$target.ilrs_id) `
    --start '2026-11-16T09:00:00Z' --duration 14400 --step 30 `
    --source-url $source.sourceUrl --retrieved-at $source.retrievedAtUtc
    if ($LASTEXITCODE -ne 0) { throw "Snapshot export failed: $($source.key)" }
}
