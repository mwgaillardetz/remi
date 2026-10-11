using System;
using System.Windows;
using System.Windows.Input;
using Remi.Desktop.Character;

namespace Remi.Desktop;

public partial class ChatWindow : Window
{
    private readonly CompanionController controller;
    public ChatWindow(CompanionController controller)
    {
        InitializeComponent(); this.controller = controller;
        Conversation.ItemsSource = controller.Messages;
        controller.Messages.CollectionChanged += (_, _) => RefreshMessages();
        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(this);
        IsVisibleChanged += (_, _) => { if (IsVisible) Composer.Focus(); };
    }

    public void RefreshMessages()
    {
        Welcome.Visibility = controller.Messages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (controller.Messages.Count > 0) Conversation.ScrollIntoView(controller.Messages[^1]);
    }
    public void SetStatus(string? text, bool error = false)
    {
        Status.Text = text ?? ""; Status.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        Status.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, error ? "Error" : "Muted");
    }
    public void SetBusy(bool busy, bool recording = false)
    {
        SendButton.Content = busy ? "■" : "↑";
        SendButton.ToolTip = busy ? "Stop" : "Send message";
        System.Windows.Automation.AutomationProperties.SetName(SendButton, busy ? "Stop" : "Send message");
        MicIcon.Data = System.Windows.Media.Geometry.Parse(recording ? "M3,3 L21,3 21,21 3,21 Z" : "M9,2 Q9,0 12,0 Q15,0 15,2 L15,10 Q15,13 12,13 Q9,13 9,10 Z M5,9 L7,9 Q7,16 12,16 Q17,16 17,9 L19,9 Q19,17 13,18 L13,21 17,21 17,23 7,23 7,21 11,21 11,18 Q5,17 5,9 Z");
        MicButton.ToolTip = recording ? "Finish recording" : "Record a voice message";
        System.Windows.Automation.AutomationProperties.SetName(MicButton, recording ? "Finish recording" : "Record a voice message");
        MicButton.IsEnabled = !busy || recording;
        JokeButton.IsEnabled = FactButton.IsEnabled = SongButton.IsEnabled = !busy;
    }
    public void RefreshConnection()
    {
        if (controller.Settings.Provider == "openai") { ConnectionLabel.Text = $"OpenAI · {controller.Settings.OpenAiModel}"; return; }
        var local = new Uri(controller.Settings.Host).IsLoopback;
        ConnectionLabel.Text = $"{(local ? "Local Ollama" : new Uri(controller.Settings.Host).Host)} · {controller.Settings.Model}";
    }
    private async void SendClick(object sender, RoutedEventArgs e)
    {
        if (controller.IsBusy) { controller.Cancel(); return; }
        var text = Composer.Text.Trim(); if (text.Length == 0) return;
        Composer.Clear(); await controller.SendAsync(text);
    }
    private void ComposerKeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { e.Handled = true; SendClick(sender, e); } }
    private async void JokeClick(object sender, RoutedEventArgs e) => await controller.SendAsync("Tell me a short joke!", Mood.Excited);
    private async void FactClick(object sender, RoutedEventArgs e) => await controller.SendAsync("Give me a surprising fun fact!", Mood.Excited);
    private async void SongClick(object sender, RoutedEventArgs e) => await controller.SendAsync("Sing me a few lines of In My Merry Oldsmobile!", Mood.Singing);
    private async void MicClick(object sender, RoutedEventArgs e) => await controller.ToggleRecordingAsync();
    private void SettingsClick(object sender, RoutedEventArgs e) => controller.OpenSettings();
    private void CloseClick(object sender, RoutedEventArgs e) => controller.CloseChat();
    private void ClearClick(object sender, RoutedEventArgs e) => controller.ClearChat();
    private void WindowKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { controller.CloseChat(); e.Handled = true; } }
    private void HeaderDown(object sender, MouseButtonEventArgs e)
    { if (e.OriginalSource is System.Windows.Controls.TextBlock or System.Windows.Controls.Grid) { DragMove(); NativeMethods.KeepOnScreen(this); } }
}
