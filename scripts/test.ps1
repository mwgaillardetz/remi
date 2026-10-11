$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
& dotnet run --project (Join-Path $workspace 'tests/Remi.Core.Tests') -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet build (Join-Path $workspace 'native/Remi.Desktop/Remi.Desktop.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$executable = Join-Path $workspace 'native/Remi.Desktop/bin/Release/net10.0-windows/Remi.exe'
$output = Join-Path $workspace 'artifacts/preview'
$smoke = Start-Process -FilePath $executable -ArgumentList @('--smoke-test', "--output=`"$output`"") -WindowStyle Hidden -PassThru
if (-not $smoke.WaitForExit(30000)) { Stop-Process -Id $smoke.Id; throw 'Native smoke test timed out.' }
if ($smoke.ExitCode -ne 0) { throw "Native smoke test failed. See $env:LOCALAPPDATA\Remi\startup-error.log" }
Get-Content -LiteralPath (Join-Path $output 'smoke-result.txt')
