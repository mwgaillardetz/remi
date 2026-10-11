using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Remi.Core;
using Remi.Desktop.Services;

namespace Remi.Desktop;

public partial class SettingsWindow : Window
{
    private readonly CompanionController controller;
    private readonly CancellationTokenSource lifetime = new();
    private bool ready;
    private bool removeKey;
    public SettingsWindow(CompanionController controller)
    {
        InitializeComponent(); this.controller = controller;
        var saved = controller.Settings;
        Populate(saved);
        try { KeyStatus.Text = string.IsNullOrEmpty(controller.OpenAiKey()) ? "Enter a key to connect." : "A key is available. Leave blank to keep it, or enter a replacement."; }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or IOException or UnauthorizedAccessException)
        { KeyStatus.Text = "The saved key could not be read. Enter a replacement or remove it."; }
        try
        {
            VoiceBox.ItemsSource = new[] { "Windows default voice" }.Concat(controller.Voice.Voices()).ToArray();
            VoiceBox.SelectedItem = string.IsNullOrEmpty(saved.Voice) ? "Windows default voice" : saved.Voice;
            if (VoiceBox.SelectedIndex < 0) VoiceBox.SelectedIndex = 0;
            MicrophoneBox.ItemsSource = VoiceService.Microphones(); MicrophoneBox.SelectedValue = saved.Microphone;
            if (MicrophoneBox.SelectedIndex < 0) MicrophoneBox.SelectedIndex = 0;
        }
        catch (Exception ex) { Status.Text = "Some audio devices are unavailable: " + ex.Message; }
        ready = true; PreviewChanged(this, new RoutedEventArgs());
        ProviderChanged(this, new RoutedEventArgs());
        Closed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); ApiKeyBox.Clear(); ThemeManager.Apply(controller.Settings); };
    }

    private void Populate(Settings saved)
    {
        HostBox.Text = saved.Host; ModelBox.Text = saved.Model;
        ProviderBox.SelectedIndex = saved.Provider == "openai" ? 1 : 0; OpenAiModelBox.Text = saved.OpenAiModel;
        CharacterBox.SelectedIndex = Array.IndexOf(new[] { "professional", "cute", "robot" }, saved.CharacterStyle);
        ThemeBox.SelectedIndex = Array.IndexOf(new[] { "eucalyptus", "slate", "midnight" }, saved.Theme);
        AccentBox.Text = saved.AccentColor; BackgroundBox.Text = saved.BackgroundColor;
        FurBox.SelectedIndex = Array.IndexOf(new[] { "grey", "amber", "black" }, saved.Fur);
        OutfitBox.SelectedIndex = Array.IndexOf(new[] { "none", "plain", "hawaiian", "tuxedo" }, saved.Outfit);
        SizeSlider.Value = saved.Scale; GlassesBox.IsChecked = saved.Sunglasses; MotionBox.IsChecked = saved.ReducedMotion;
        SpeechBox.IsChecked = saved.SpeechEnabled; RateSlider.Value = saved.SpeechRate; UnloadBox.IsChecked = saved.UnloadModel;
        PythonBox.Text = saved.PythonPath; WhisperBox.SelectedIndex = Array.IndexOf(new[] { "tiny", "base", "small" }, saved.WhisperModel);
        if (VoiceBox.ItemsSource != null)
        {
            VoiceBox.SelectedItem = string.IsNullOrEmpty(saved.Voice) ? "Windows default voice" : saved.Voice;
            if (VoiceBox.SelectedIndex < 0) VoiceBox.SelectedIndex = 0;
            MicrophoneBox.SelectedValue = saved.Microphone;
        }
    }

    private Settings Collect() => controller.Settings with
    {
        Host = HostBox.Text, Model = ModelBox.Text,
        Provider = ProviderBox.SelectedIndex == 1 ? "openai" : "ollama", OpenAiModel = OpenAiModelBox.Text,
        CharacterStyle = new[] { "professional", "cute", "robot" }[Math.Max(0, CharacterBox.SelectedIndex)],
        Theme = new[] { "eucalyptus", "slate", "midnight" }[Math.Max(0, ThemeBox.SelectedIndex)],
        AccentColor = AccentBox.Text, BackgroundColor = BackgroundBox.Text,
        Fur = new[] { "grey", "amber", "black" }[Math.Max(0, FurBox.SelectedIndex)],
        Outfit = new[] { "none", "plain", "hawaiian", "tuxedo" }[Math.Max(0, OutfitBox.SelectedIndex)],
        Scale = SizeSlider.Value, Sunglasses = GlassesBox.IsChecked == true, ReducedMotion = MotionBox.IsChecked == true,
        SpeechEnabled = SpeechBox.IsChecked == true, SpeechRate = (int)RateSlider.Value,
        Voice = VoiceBox.SelectedIndex > 0 ? (string)VoiceBox.SelectedItem : "",
        Microphone = MicrophoneBox.SelectedValue is int device ? device : -1,
        PythonPath = PythonBox.Text, WhisperModel = ((ComboBoxItem?)WhisperBox.SelectedItem)?.Content.ToString() ?? "base",
        UnloadModel = UnloadBox.IsChecked == true
    };
    private void PreviewChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        var value = Collect(); PreviewCharacter.Configure(value with { ReducedMotion = true });
        ClothingOptions.Visibility = GlassesBox.Visibility = value.CharacterStyle == "robot" ? Visibility.Collapsed : Visibility.Visible;
    }
    private void ThemeChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        try { ThemeManager.Apply(Collect()); Status.Text = "Theme preview. Save to keep, or cancel to restore."; }
        catch (ArgumentException) { Status.Text = "Enter a six-digit color such as #426B58, or leave blank."; }
    }
    private void ResetColorsClick(object sender, RoutedEventArgs e) { AccentBox.Clear(); BackgroundBox.Clear(); }
    private void ProviderChanged(object sender, RoutedEventArgs e)
    { if (ready) { OllamaPanel.Visibility = ProviderBox.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed; OpenAiPanel.Visibility = ProviderBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed; } }
    private void RemoveKeyClick(object sender, RoutedEventArgs e)
    { removeKey = true; ApiKeyBox.Clear(); KeyStatus.Text = "Saved key will be removed when you save. An OPENAI_API_KEY environment variable, if set, remains available."; }
    private async void RefreshClick(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async Task RefreshAsync()
    {
        RefreshButton.IsEnabled = OpenAiRefreshButton.IsEnabled = ProviderBox.IsEnabled = HostBox.IsEnabled = false; Status.Text = "Looking for models…";
        var token = lifetime.Token;
        try
        {
            var openAi = ProviderBox.SelectedIndex == 1;
            var box = openAi ? OpenAiModelBox : ModelBox;
            var selection = box.Text;
            var models = openAi ? await controller.RefreshOpenAiModelsAsync(ApiKeyBox.Password.Length > 0 ? ApiKeyBox.Password.Trim() : removeKey ? "" : null, token) : await controller.RefreshModelsAsync(Settings.NormalizeHost(HostBox.Text), token);
            if (token.IsCancellationRequested) return;
            box.ItemsSource = models;
            box.Text = openAi || models.Contains(selection) ? selection : models.FirstOrDefault() ?? selection;
            Status.Text = models.Length == 0 ? "No compatible models found. Check your provider account or install an Ollama model." : $"Found {models.Length} model{(models.Length == 1 ? "" : "s")}.";
        }
        catch (OperationCanceledException) { if (!token.IsCancellationRequested) Status.Text = "The provider did not respond. Check your connection and try again."; }
        catch (Exception ex) { Status.Text = "Could not load models: " + ex.Message; }
        finally { RefreshButton.IsEnabled = OpenAiRefreshButton.IsEnabled = ProviderBox.IsEnabled = HostBox.IsEnabled = true; }
    }
    private void SaveClick(object sender, RoutedEventArgs e)
    { try { var key = ApiKeyBox.Password.Trim(); controller.ApplySettings(Collect(), key.Length == 0 ? null : key, removeKey && key.Length == 0); Close(); } catch (Exception ex) { Status.Text = "Could not save: " + ex.Message; } }
    private void CancelClick(object sender, RoutedEventArgs e) => Close();
    private void AddVoicesClick(object sender, RoutedEventArgs e)
    { try { Process.Start(new ProcessStartInfo("ms-settings:speech") { UseShellExecute = true }); } catch (Exception ex) { Status.Text = ex.Message; } }
}
