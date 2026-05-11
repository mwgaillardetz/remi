# Rémi

Your new favorite AI desktop companion - inspired by Bonzibuddy without the spyware. He can even
serenade you with the hit classic, "In My Merry Oldsmobile"!

## What he does

- Floats on your desktop as a transparent, always-on-top window
- Drag him anywhere on screen. Not too fast
- Click him to open a chat panel below
- Talks back using your system's text-to-speech
- Talk to him by using the microphone toggle
- Powered by a local or cloud (recommended) Ollama model

## Requirements

- Node.js 18+
- Ollama
- ffmpeg (for voice input)
- Python + openai-whisper (for voice input)

## Setup

### 1. Install Ollama

Download and install Ollama from [ollama.com](https://ollama.com). On Windows via PowerShell:

```powershell
irm https://ollama.com/install.ps1 | iex
```

Pull a model. A cloud-hosted or larger model will give noticeably better conversation quality — something like `gemma3:4b-cloud`. For local use, `llama3.2` works fine:

```bash
ollama pull llama3.2
```

### 2. Install ffmpeg

ffmpeg is required to process microphone recordings. Without it, voice input will not work but text chat will still function.

```bash
winget install ffmpeg
```

Or via Chocolatey:

```bash
choco install ffmpeg
```

Verify:

```bash
ffmpeg -version
```

### 3. Install Whisper

Whisper handles speech-to-text locally. Requires Python 3.8+.

```bash
pip install openai-whisper
```

Note: `numpy<2` pin may be required due to a compatibility issue between the current numpy release and whisper's torch dependency.

### 4. Install dependencies

```bash
npm install
```

## Running

```bash
npm run electron:dev
```

## Building

```bash
npm run electron:build
```

## Settings

Open the chat panel and click the gear icon to change:

- Ollama model/optional target host
- Voice (text-to-speech) - for windows, download more voices if you want a little more pizazz https://support.microsoft.com/en-us/topic/download-languages-and-voices-for-immersive-reader-read-mode-and-read-aloud-4c83a8d8-7486-42f7-8e46-2b0fdf753130
- Speaking speed
- Microphone input
- Customize appearence

## Keyboard shortcut

`Ctrl+Shift+B` toggles Rémi's visibility.


## License

MIT — see [LICENSE](LICENSE)

## Support

[![Buy Me a Coffee](https://img.shields.io/badge/Buy%20Me%20a%20Coffee-donate-yellow)](https://buymeacoffee.com/mgaillardetz)
