[CmdletBinding()]
param(
    [ValidateSet('inspect', 'self-test', 'listen', 'test-page', 'help', '--version')]
    [string]$Mode = 'inspect',
    [ValidateRange(1, 3600)]
    [int]$Seconds = 30
)
$ErrorActionPreference = 'Stop'
$controllerDll = Join-Path $PSScriptRoot 'out/D200xDirectController.dll'
if (-not (Test-Path -LiteralPath $controllerDll -PathType Leaf)) {
    throw 'Build first from this repository: ./build.ps1'
}
$dotnetCommand = Get-Command dotnet -ErrorAction Stop
$controllerArgs = @($controllerDll, $Mode)
if ($Mode -in @('listen', 'test-page')) { $controllerArgs += $Seconds }
& $dotnetCommand.Source @controllerArgs
if ($LASTEXITCODE -ne 0) { throw "Direct controller exited with code $LASTEXITCODE." }
