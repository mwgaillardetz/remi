param([string]$Executable)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
if (-not $Executable) {
    & dotnet build (Join-Path $workspace 'native/Remi.Desktop/Remi.Desktop.csproj') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $Executable = Join-Path $workspace 'native/Remi.Desktop/bin/Release/net10.0-windows/Remi.exe'
}
$output = Join-Path $workspace 'artifacts/benchmark'
$benchmark = Start-Process -FilePath $Executable -ArgumentList @('--benchmark', "--output=`"$output`"") -WindowStyle Hidden -PassThru
if (-not $benchmark.WaitForExit(40000)) { Stop-Process -Id $benchmark.Id; throw 'Idle benchmark timed out.' }
if ($benchmark.ExitCode -ne 0) { throw 'Idle benchmark failed. See startup-error.log in the Remi settings folder.' }
Get-Content -LiteralPath (Join-Path $output 'idle.json')
