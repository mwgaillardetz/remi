using System.Text.Json;

namespace Remi.Core;

public static class SettingsMigration
{
    /// <summary>Imports an explicit export of Electron's remi-settings localStorage value.</summary>
    public static Settings FromElectronJson(string json, Settings? defaults = null)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new ArgumentException("Select a JSON object exported from Rémi's remi-settings entry.");
        var initial = defaults ?? new Settings();
        string Text(string key, string fallback) => root.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() ?? fallback : fallback;
        bool Flag(string key, bool fallback) => root.TryGetProperty(key, out var item) && item.ValueKind is JsonValueKind.True or JsonValueKind.False ? item.GetBoolean() : fallback;
        double Number(string key, double fallback) => root.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out var value) ? value : fallback;
        var browserRate = Math.Clamp(Number("ttsRate", 1), .5, 2);
        return (initial with
        {
            Host = Text("ollamaHost", initial.Host), Model = Text("model", initial.Model),
            SpeechEnabled = Flag("ttsEnabled", initial.SpeechEnabled), Voice = Text("selectedVoice", initial.Voice),
            SpeechRate = (int)Math.Round(Math.Log2(browserRate) * 5), Scale = Number("scale", initial.Scale),
            Fur = Text("furColor", initial.Fur), Sunglasses = Flag("showSunglasses", initial.Sunglasses),
            Outfit = Flag("showShirt", false) ? Text("shirtStyle", "hawaiian") : "none",
            Microphone = -1 // Browser device identifiers are not native audio device numbers.
        }).Normalize();
    }
}
