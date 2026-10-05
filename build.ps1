[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'DirectController.csproj'
$output = Join-Path $PSScriptRoot 'out'
$dotnetCommand = Get-Command dotnet -ErrorAction Stop
& $dotnetCommand.Source publish $project -c Release --self-contained false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
& $dotnetCommand.Source (Join-Path $output 'D200xDirectController.dll') self-test
if ($LASTEXITCODE -ne 0) { throw 'Automated checks failed.' }
& $dotnetCommand.Source build (Join-Path $PSScriptRoot 'Desktop/D200xDirect.App.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Desktop app build failed.' }
