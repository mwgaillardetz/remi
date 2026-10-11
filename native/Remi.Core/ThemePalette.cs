using System.Globalization;

namespace Remi.Core;

public sealed record ThemePalette(string Paper, string Surface, string Subtle, string Border, string Ink,
    string Muted, string Accent, string AccentInk, string AccentText, string UserBubble, string Error)
{
    public static string NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        value = value.Trim();
        if (value.Length != 7 || value[0] != '#' || !value[1..].All(Uri.IsHexDigit))
            throw new ArgumentException("Use a six-digit color such as #426B58, or leave it blank for the theme default.");
        return value.ToUpperInvariant();
    }

    public static ThemePalette FromSettings(Settings settings)
    {
        var (background, accent) = settings.Theme switch
        {
            "midnight" => ("#17232B", "#7CB8BA"),
            "slate" => ("#F2F4F7", "#3C647D"),
            _ => ("#F8F7F2", "#426B58")
        };
        background = NormalizeColor(settings.BackgroundColor) is { Length: > 0 } b ? b : background;
        accent = NormalizeColor(settings.AccentColor) is { Length: > 0 } a ? a : accent;
        var dark = Luminance(background) < .179;
        var preferredInk = dark ? "#F2F5F6" : "#1D2B31";
        var ink = Contrast(background, preferredInk) >= 4.5 ? preferredInk : Contrast(background, "#FFFFFF") >= Contrast(background, "#000000") ? "#FFFFFF" : "#000000";
        string ReadableSurface(string candidate) => Contrast(ink, candidate) >= 4.5 ? candidate : background;
        var surface = ReadableSurface(Blend(background, "#FFFFFF", dark ? .05 : .55));
        var subtle = ReadableSurface(Blend(background, ink, .055));
        var muted = Blend(background, ink, .68);
        return new ThemePalette(background, surface, subtle, Blend(background, ink, .20),
            ink, Contrast(background, muted) >= 4.5 ? muted : ink, accent, Contrast(accent, "#FFFFFF") >= Contrast(accent, "#000000") ? "#FFFFFF" : "#000000",
            Contrast(accent, background) >= 4.5 ? accent : ink, ReadableSurface(Blend(background, accent, .18)), dark ? "#FFB19D" : "#9D4834");
    }

    public static double Contrast(string first, string second)
    { var a = Luminance(first); var b = Luminance(second); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05); }
    private static double Luminance(string color)
    {
        static double Linear(int channel) { var v = channel / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
        var c = Channels(color); return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
    }
    private static (int R, int G, int B) Channels(string color) => (int.Parse(color[1..3], NumberStyles.HexNumber), int.Parse(color[3..5], NumberStyles.HexNumber), int.Parse(color[5..7], NumberStyles.HexNumber));
    private static string Blend(string first, string second, double ratio)
    {
        var a = Channels(first); var b = Channels(second);
        int Channel(int x, int y) => (int)Math.Round(x + (y - x) * ratio);
        return $"#{Channel(a.R, b.R):X2}{Channel(a.G, b.G):X2}{Channel(a.B, b.B):X2}";
    }
}
