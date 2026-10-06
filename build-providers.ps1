[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskCargo = Get-Command cargo -ErrorAction SilentlyContinue
if (-not $taskCargo) {
    $taskLocalCargo = Join-Path $PSScriptRoot '.tools/cargo/bin/cargo.exe'
    if (-not (Test-Path -LiteralPath $taskLocalCargo -PathType Leaf)) { throw 'Install the pinned Rust toolchain, or provide cargo on PATH.' }
    $env:RUSTUP_HOME = Join-Path $PSScriptRoot '.tools/rustup'
    $env:CARGO_HOME = Join-Path $PSScriptRoot '.tools/cargo'
    $taskCargoPath = $taskLocalCargo
} else { $taskCargoPath = $taskCargo.Source }
Push-Location (Join-Path $PSScriptRoot 'providers')
try {
    & $taskCargoPath fmt --all -- --check
    if ($LASTEXITCODE -ne 0) { throw 'Rust formatting check failed.' }
    & $taskCargoPath clippy --workspace --all-targets --locked -- -D warnings
    if ($LASTEXITCODE -ne 0) { throw 'Rust lint failed.' }
    & $taskCargoPath test --workspace --locked
    if ($LASTEXITCODE -ne 0) { throw 'Rust tests failed.' }
    & $taskCargoPath build --workspace --release --locked
    if ($LASTEXITCODE -ne 0) { throw 'Rust release build failed.' }
} finally { Pop-Location }
$taskMetadataOutput = & $taskCargoPath metadata --manifest-path (Join-Path $PSScriptRoot 'providers/Cargo.toml') --format-version 1 --locked
if ($LASTEXITCODE -ne 0) { throw 'Could not inventory Rust dependencies.' }
$taskMetadata = ($taskMetadataOutput -join [Environment]::NewLine) | ConvertFrom-Json
$taskDependencies = @($taskMetadata.packages | Where-Object { $_.source -like 'registry+*' })
foreach ($taskName in @('system', 'fixture')) {
    $taskDestination = Join-Path $PSScriptRoot "out/provider-packages/$taskName"
    New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "providers/packages/$taskName/plugin.json") -Destination $taskDestination -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "providers/target/release/d200x-$taskName.exe") -Destination $taskDestination -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $taskDestination -Force
    $taskNotices = @('Rust dependency inventory from the exact Cargo.lock. Upstream licenses apply separately from project MIT.', '')
    foreach ($taskDependency in $taskDependencies) {
        $taskLicenseDirectory = Join-Path $taskDestination "licenses/$($taskDependency.name)-$($taskDependency.version)"
        New-Item -ItemType Directory -Path $taskLicenseDirectory -Force | Out-Null
        $taskLicenseFiles = @(Get-ChildItem -LiteralPath (Split-Path $taskDependency.manifest_path -Parent) -File | Where-Object { $_.Name -match '^(LICENSE|COPYING|NOTICE)' })
        if ($taskLicenseFiles.Count -eq 0) { throw "Missing dependency license: $($taskDependency.name)" }
        foreach ($taskLicenseFile in $taskLicenseFiles) { Copy-Item -LiteralPath $taskLicenseFile.FullName -Destination $taskLicenseDirectory -Force }
        $taskNotices += "$($taskDependency.name) $($taskDependency.version): $($taskDependency.license) | $($taskDependency.repository)"
    }
    $taskNotices | Set-Content -LiteralPath (Join-Path $taskDestination 'THIRD_PARTY_NOTICES.txt') -Encoding utf8
}
$taskRuntimeSystem = Join-Path $PSScriptRoot 'out/runtime-plugins/system'
New-Item -ItemType Directory -Path $taskRuntimeSystem -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'out/provider-packages/system') | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $taskRuntimeSystem -Recurse -Force
}
Write-Host 'Provider packages built in out/provider-packages. Discovery does not start them.'
