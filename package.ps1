[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$dotnetCommand = Get-Command dotnet -ErrorAction Stop
[xml]$projectDocument = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'DirectController.csproj') -Raw
$version = [string]$projectDocument.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$') { throw 'Unexpected package version.' }
$workDirectory = Join-Path $PSScriptRoot "out/package-$version"
$appDirectory = Join-Path $workDirectory 'App'
$setupDirectory = Join-Path $workDirectory 'Setup'
$releaseDirectory = Join-Path $PSScriptRoot 'releases'
New-Item -ItemType Directory -Path $workDirectory, $releaseDirectory -Force | Out-Null

# Reuse the existing SDK. Restore may fetch runtime build packs; it installs no system runtime.
& $dotnetCommand.Source publish (Join-Path $PSScriptRoot 'Desktop/D200xDirect.App.csproj') -c Release -r win-x64 --self-contained true -p:DebugType=None -o $appDirectory
if ($LASTEXITCODE -ne 0) { throw 'App publish failed.' }
foreach ($name in @('profiles', 'docs')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $appDirectory -Recurse -Force
}
foreach ($name in @('README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'CHANGELOG.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $appDirectory -Force
}

$runtimeConfig = Get-Content -LiteralPath (Join-Path $appDirectory 'D200xDirect.runtimeconfig.json') -Raw | ConvertFrom-Json
$cacheOutput = & $dotnetCommand.Source nuget locals global-packages --list
if ($LASTEXITCODE -ne 0) { throw 'Could not locate the NuGet build cache.' }
$cacheMatch = [regex]::Match(($cacheOutput -join "`n"), 'global-packages:\s*(.+)')
if (-not $cacheMatch.Success) { throw 'Could not parse the NuGet build cache location.' }
$packageCache = $cacheMatch.Groups[1].Value.Trim()
foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
    if ($framework.name -eq 'Microsoft.NETCore.App') { $packName = 'microsoft.netcore.app.runtime.win-x64'; $prefix = 'DOTNET' }
    elseif ($framework.name -eq 'Microsoft.WindowsDesktop.App') { $packName = 'microsoft.windowsdesktop.app.runtime.win-x64'; $prefix = 'WINDOWSDESKTOP' }
    else { throw "Unexpected runtime framework: $($framework.name)" }
    $packDirectory = Join-Path (Join-Path $packageCache $packName) $framework.version
    $licenseSource = @('LICENSE.txt', 'LICENSE') | ForEach-Object { Join-Path $packDirectory $_ } | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $licenseSource) { throw "Missing runtime license in $packDirectory" }
    Copy-Item -LiteralPath $licenseSource -Destination (Join-Path $appDirectory "$prefix-LICENSE.txt") -Force
    $noticeSource = Join-Path $packDirectory 'THIRD-PARTY-NOTICES.txt'
    if (Test-Path -LiteralPath $noticeSource -PathType Leaf) {
        Copy-Item -LiteralPath $noticeSource -Destination (Join-Path $appDirectory "$prefix-THIRD-PARTY-NOTICES.txt") -Force
    } elseif ($prefix -eq 'DOTNET') { throw "Missing runtime notices: $noticeSource" }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$payload = Join-Path $workDirectory 'App.zip'
# Create/replace only our specific generated archive; do not delete arbitrary folders.
if (Test-Path -LiteralPath $payload -PathType Leaf) {
    $archiveStream = [IO.File]::Open($payload, [IO.FileMode]::Create)
    $archiveStream.Dispose()
    $zip = [IO.Compression.ZipFile]::Open($payload, [IO.Compression.ZipArchiveMode]::Update)
} else { $zip = [IO.Compression.ZipFile]::Open($payload, [IO.Compression.ZipArchiveMode]::Create) }
try {
    foreach ($file in Get-ChildItem -LiteralPath $appDirectory -File -Recurse | Where-Object {
        $_.Name -notlike 'ui-check*' -and $_.FullName -notmatch '[\\/](ui-check-profile)[\\/]'
    }) {
        $entry = $file.FullName.Substring($appDirectory.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $zip.Dispose() }

& $dotnetCommand.Source publish (Join-Path $PSScriptRoot 'Installer/D200xDirect.Setup.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None "-p:PayloadPath=$payload" -o $setupDirectory
if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }
$installer = Join-Path $setupDirectory 'D200xDirect-Setup.exe'
$check = Start-Process -FilePath $installer -ArgumentList '--check-payload' -WindowStyle Hidden -Wait -PassThru
if ($check.ExitCode -ne 0) { throw 'Installer payload verification failed.' }
$setupRelease = Join-Path $releaseDirectory "D200X-Direct-$version-win-x64-Setup.exe"
Copy-Item -LiteralPath $installer -Destination $setupRelease -Force

# Export only project source/docs. No .git, runtime builds, personal profiles or machine logs.
$sourceArchive = Join-Path $releaseDirectory "D200X-Direct-$version-source.zip"
$sourceStream = [IO.File]::Open($sourceArchive, [IO.FileMode]::Create)
$sourceZip = New-Object IO.Compression.ZipArchive($sourceStream, [IO.Compression.ZipArchiveMode]::Create, $false)
try {
    $rootFiles = Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Name -in @('.gitignore', '.gitattributes', 'LICENSE') -or $_.Extension -in @('.cs', '.csproj', '.ps1', '.md', '.cmd') }
    $nestedFiles = foreach ($folder in @('Desktop', 'Installer', 'profiles', 'docs')) {
        Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $folder) -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
    }
    foreach ($file in @($rootFiles) + @($nestedFiles)) {
        $entry = $file.FullName.Substring($PSScriptRoot.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($sourceZip, $file.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $sourceZip.Dispose(); $sourceStream.Dispose() }
$checksums = Join-Path $releaseDirectory "D200X-Direct-$version-SHA256.txt"
@($setupRelease, $sourceArchive) | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_))"
} | Set-Content -LiteralPath $checksums -Encoding Ascii
Write-Host "Installer: $setupRelease"
Write-Host "Source:    $sourceArchive"
Write-Host "Checksums: $checksums"
