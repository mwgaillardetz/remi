using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Remi.Core;

namespace Remi.Desktop;

internal static class ThemeManager
{
    internal static void Apply(Settings settings)
    {
        var palette = ThemePalette.FromSettings(settings);
        foreach (var property in typeof(ThemePalette).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString((string)property.GetValue(palette)!));
            brush.Freeze(); Application.Current.Resources[property.Name] = brush;
        }
        Application.Current.Resources[System.Windows.SystemColors.WindowBrushKey] = Application.Current.Resources["Surface"];
        Application.Current.Resources[System.Windows.SystemColors.WindowTextBrushKey] = Application.Current.Resources["Ink"];
        Application.Current.Resources[System.Windows.SystemColors.ControlTextBrushKey] = Application.Current.Resources["Ink"];
    }
}
