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

## Run

Windows is required.

For a packaged build, extract the entire ZIP and run `Remi.exe`. The small `desktop-runtime` package requires the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0); the larger `standalone` package includes it. Keep the `Voice` folder alongside the executable for microphone input.

From source, install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), then:

```powershell
dotnet run --project native/Remi.Desktop -c Release
```

## Ollama

Install [Ollama](https://ollama.com/download) and pull a model:

```powershell
ollama pull qwen3.8
```

The default host is `http://127.0.0.1:11434`. Open **Settings → Connection** to set a host and model; **Refresh available models** lists installed models. If the selected model is unavailable, Rémi selects an available model and shows its name in chat.

## OpenAI / ChatGPT models

In **Settings → Connection**, select **OpenAI · ChatGPT models**, enter an OpenAI API key, and save. This connection uses the OpenAI API and its separate usage billing. It does not sign into the ChatGPT application. `gpt-4.1-mini` is the initial model; type another Responses-compatible model ID or refresh the list available to your API account.

Keys are encrypted with Windows DPAPI for the current user in `%LOCALAPPDATA%\Remi\openai-key.bin`, separate from preferences. Leave the key field blank to retain the saved key, enter a replacement, or choose **Remove saved key** and save. `OPENAI_API_KEY` is also supported as a fallback; removing a saved key does not remove that environment variable.

## Optional local voice input

Text chat and speech output work without Python. For microphone input, install Python and Whisper:

```powershell
python -m pip install openai-whisper
```

Choose a microphone, Python executable, and `tiny`, `base`, or `small` Whisper model in **Settings → Voice**. The first transcription downloads the selected model; later transcriptions can run offline. `tiny` uses the fewest resources. No microphone is opened at launch.

## License and support

MIT — see [LICENSE](LICENSE). [Buy me a coffee](https://buymeacoffee.com/mgaillardetz).
