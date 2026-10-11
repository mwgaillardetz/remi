param([ValidateSet('tiny', 'base', 'small')][string]$Model = 'tiny')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$output = Join-Path $workspace 'artifacts/voice-test'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$audioPath = Join-Path $output 'fixture.wav'
$textPath = Join-Path $output 'fixture.txt'
Add-Type -AssemblyName System.Speech
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo 16000, ([System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen), ([System.Speech.AudioFormat.AudioChannel]::Mono)
    $speaker.SetOutputToWaveFile($audioPath, $format)
    $speaker.Speak('Hello Remi. It is a lovely day.')
} finally { $speaker.Dispose() }
& python (Join-Path $workspace 'native/Remi.Desktop/Voice/transcribe.py') --audio $audioPath --model $Model --output $textPath
if ($LASTEXITCODE -ne 0) { throw 'Whisper voice integration failed.' }
$transcript = Get-Content -LiteralPath $textPath -Raw
if ($transcript -notmatch 'lovely day') { throw "Unexpected transcript: $transcript" }
Write-Output "PASS: native speech output to PCM and real local Whisper transcription: $transcript"
