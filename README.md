# Rémi

Your new favorite AI desktop companion - inspired by Bonzibuddy without the spyware.

## What he does

- Floats on your desktop as a transparent, always-on-top window
- Drag him anywhere on screen
- Click him to open a chat panel below
- Talks back using your system's text-to-speech
- Listens via your microphone (toggle switch)
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
pip install openai-whisper "numpy<2"
```

The `numpy<2` pin is required due to a compatibility issue between the current numpy release and whisper's torch dependency.

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

- Ollama model
- Voice (text-to-speech)
- Speaking speed
- Microphone input

## Keyboard shortcut

`Ctrl+Shift+B` toggles Rémi's visibility.

## Pointing at a remote Ollama instance

If you have Ollama running on another machine or a cloud server, set the host before launching:

```bash
# Windows
set OLLAMA_HOST=http://your-server:11434
npm run electron:dev
```

Then select your preferred model from the settings panel inside the app.
