# Requires the original Electron installation and tracked historical dist output.
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$electron = Join-Path $workspace 'node_modules/electron/dist/electron.exe'
if (-not (Test-Path -LiteralPath $electron)) { throw 'The archived Electron installation is unavailable. This optional baseline does not install it.' }
$output = Join-Path $workspace 'artifacts/electron-baseline'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$ready = Join-Path $output 'ready.json'
if (Test-Path -LiteralPath $ready) { Remove-Item -LiteralPath $ready }
$baseline = Start-Process -FilePath $electron -ArgumentList "`"$(Join-Path $workspace 'legacy/electron/benchmark.cjs')`"" -WindowStyle Hidden -PassThru
try {
    $wait = [System.Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path -LiteralPath $ready)) {
        if ($wait.Elapsed.TotalSeconds -gt 20 -or $baseline.HasExited) { throw 'Historical renderer failed to start.' }
        Start-Sleep -Milliseconds 100
    }
    $metadata = Get-Content -LiteralPath $ready -Raw | ConvertFrom-Json
    $processes = @($metadata.pids | ForEach-Object { Get-Process -Id $_ })
    $startCpu = ($processes | Measure-Object -Property CPU -Sum).Sum
    $clock = [System.Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Seconds 12
    foreach ($process in $processes) { $process.Refresh() }
    $endCpu = ($processes | Measure-Object -Property CPU -Sum).Sum
    $result = [ordered]@{
        measuredAtUtc = [DateTime]::UtcNow.ToString('o')
        electron = $metadata.electron
        scenario = 'Historical 400x500 Three.js pet visible, chat closed, microphone denied, no inference, isolated profile; minimal main-process harness'
        processCount = $processes.Count
        durationSeconds = [Math]::Round($clock.Elapsed.TotalSeconds, 2)
        cpuPercentOfOneCore = [Math]::Round(($endCpu - $startCpu) / $clock.Elapsed.TotalSeconds * 100, 3)
        workingSetMiB = [Math]::Round(($processes | Measure-Object -Property WorkingSet64 -Sum).Sum / 1MB, 1)
        privateMemoryMiB = [Math]::Round(($processes | Measure-Object -Property PrivateMemorySize64 -Sum).Sum / 1MB, 1)
    }
    $json = $result | ConvertTo-Json
    [System.IO.File]::WriteAllText((Join-Path $output 'idle.json'), $json)
    Write-Output $json
} finally {
    if (-not $baseline.HasExited) { $baseline.CloseMainWindow() | Out-Null; if (-not $baseline.WaitForExit(3000)) { Stop-Process -Id $baseline.Id } }
}
