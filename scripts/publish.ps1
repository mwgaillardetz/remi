param(
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64',
    [switch]$SelfContained
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$variant = if ($SelfContained) { 'standalone' } else { 'desktop-runtime' }
$output = Join-Path $workspace "artifacts/Remi-$Runtime-$variant"
$buildArtifacts = Join-Path $workspace "artifacts/build-$Runtime-$variant"
& dotnet publish (Join-Path $workspace 'native/Remi.Desktop/Remi.Desktop.csproj') -c Release -r $Runtime --self-contained $SelfContained.ToString().ToLowerInvariant() --artifacts-path $buildArtifacts --output $output -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -p:DebugType=None -p:DebugSymbols=false --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Copy-Item -LiteralPath (Join-Path $workspace 'LICENSE') -Destination $output
Copy-Item -LiteralPath (Join-Path $workspace 'README.md') -Destination $output
Copy-Item -LiteralPath (Join-Path $workspace 'docs') -Destination $output -Recurse -Force
$hostArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
if ($Runtime -eq "win-$hostArchitecture") {
    $validationOutput = Join-Path $workspace "artifacts/package-check-$Runtime-$variant"
    $validation = Start-Process -FilePath (Join-Path $output 'Remi.exe') -ArgumentList @('--smoke-test', "--output=`"$validationOutput`"") -WindowStyle Hidden -PassThru
    if (-not $validation.WaitForExit(30000)) { Stop-Process -Id $validation.Id; throw 'Published executable validation timed out.' }
    if ($validation.ExitCode -ne 0) { throw "Published executable failed validation (exit $($validation.ExitCode)); no new archive was created." }
    Write-Output 'Published executable passed native smoke validation.'
} else {
    Write-Output "Runtime validation skipped: target $Runtime differs from this process architecture ($hostArchitecture)."
}
$archive = "$output.zip"
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -CompressionLevel Optimal -Force
$size = (Get-Item -LiteralPath $archive).Length / 1MB
Write-Output ("Packaged {0} ({1:N2} MiB)" -f $archive, $size)
