using System.Text.Json;

namespace Remi.Core;

public sealed record Settings
{
    public string Provider { get; init; } = "ollama";
    public string OpenAiModel { get; init; } = "gpt-4.1-mini";
    public string CharacterStyle { get; init; } = "professional";
    public string Theme { get; init; } = "eucalyptus";
    public string AccentColor { get; init; } = "";
    public string BackgroundColor { get; init; } = "";
    public string Host { get; init; } = "http://127.0.0.1:11434";
    public string Model { get; init; } = "llama3.2";
    public bool SpeechEnabled { get; init; } = true;
    public string Voice { get; init; } = "";
    public int SpeechRate { get; init; } = 0;
    public int Microphone { get; init; } = -1;
    public double Scale { get; init; } = 100;
    public string Fur { get; init; } = "grey";
    public string Outfit { get; init; } = "none";
    public bool Sunglasses { get; init; }
    public bool ReducedMotion { get; init; }
    public string PythonPath { get; init; } = "python";
    public string WhisperModel { get; init; } = "base";
    public bool UnloadModel { get; init; } = true;
    public double? Left { get; init; }
    public double? Top { get; init; }

    public Settings Normalize() => this with
    {
        Provider = Provider == "openai" ? "openai" : "ollama",
        OpenAiModel = string.IsNullOrWhiteSpace(OpenAiModel) ? "gpt-4.1-mini" : OpenAiModel.Trim(),
        CharacterStyle = CharacterStyle is "cute" or "robot" ? CharacterStyle : "professional",
        Theme = Theme is "slate" or "midnight" ? Theme : "eucalyptus",
        AccentColor = ThemePalette.NormalizeColor(AccentColor),
        BackgroundColor = ThemePalette.NormalizeColor(BackgroundColor),
        Host = NormalizeHost(Host),
        Model = string.IsNullOrWhiteSpace(Model) ? "llama3.2" : Model.Trim(),
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, 50, 150) : 100,
        SpeechRate = Math.Clamp(SpeechRate, -10, 10),
        Microphone = Math.Max(-1, Microphone),
        Fur = Fur is "grey" or "amber" or "black" ? Fur : "grey",
        Outfit = Outfit is "plain" or "hawaiian" or "tuxedo" ? Outfit : "none",
        PythonPath = string.IsNullOrWhiteSpace(PythonPath) ? "python" : PythonPath.Trim(),
        WhisperModel = WhisperModel is "tiny" or "base" or "small" ? WhisperModel : "base",
        Left = Left is { } x && double.IsFinite(x) ? x : null,
        Top = Top is { } y && double.IsFinite(y) ? y : null
    };

    public static string NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return "http://127.0.0.1:11434";
        if (!Uri.TryCreate(host.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Enter an HTTP or HTTPS Ollama address, such as http://127.0.0.1:11434.");
        return uri.AbsoluteUri.TrimEnd('/');
    }
}

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string? LoadWarning { get; private set; }

    public Settings Load()
    {
        try
        {
            return File.Exists(path)
                ? (JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings()).Normalize()
                : new Settings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            LoadWarning = "Your settings could not be loaded. Defaults are in use; the original file is preserved until you save settings.";
            return new Settings();
        }
    }

    public void Save(Settings settings)
    {
        var normalized = settings.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(normalized, JsonOptions));
        File.Move(temporary, path, overwrite: true);
        LoadWarning = null;
    }
}
