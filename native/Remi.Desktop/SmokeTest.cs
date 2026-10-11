using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Remi.Desktop.Character;

namespace Remi.Desktop;

/// <summary>Runs the actual WPF windows without touching user settings or an Ollama server.</summary>
internal static class SmokeTest
{
    internal static async Task RunAsync(CompanionController controller, string[] arguments, bool shortcutRegistered)
    {
        controller.ApplySettings(controller.Settings with { ReducedMotion = true });
        var output = arguments.FirstOrDefault(a => a.StartsWith("--output=", StringComparison.Ordinal))?[9..]
            ?? Path.Combine(Environment.CurrentDirectory, "artifacts", "preview");
        Directory.CreateDirectory(output);
        controller.ShowChat();
        await Task.Delay(250);
        controller.Pet.UpdateLayout(); controller.Chat.UpdateLayout();
        Check(controller.Pet.Width == 280, "default pet size");
        Check(controller.Chat.Welcome.IsVisible, "empty conversation welcome");
        controller.ApplySettings(controller.Settings with { Fur = "amber", Outfit = "hawaiian", Sunglasses = true, Scale = 150 });
        Check(controller.Pet.Width == 420, "appearance and size settings");
        Save(controller.Pet, Path.Combine(output, "remi-amber.png"));
        controller.ApplySettings(controller.Settings with { Fur = "black", Outfit = "tuxedo", Sunglasses = false, Scale = 100 });
        Save(controller.Pet, Path.Combine(output, "remi-midnight.png"));
        controller.ApplySettings(controller.Settings with { Fur = "grey", Outfit = "none", Scale = 100 });
        foreach (var style in new[] { "professional", "cute", "robot" })
        {
            controller.ApplySettings(controller.Settings with { CharacterStyle = style });
            Save(controller.Pet, Path.Combine(output, "remi-" + style + ".png"));
        }
        controller.ApplySettings(controller.Settings with { CharacterStyle = "professional" });
        controller.Messages.Add(new MessageViewModel("user", "A little company while I work?"));
        controller.Messages.Add(new MessageViewModel("assistant", "Always. I'll be right here, keeping you company. One thing at a time — you've got this."));
        controller.Chat.UpdateLayout();
        Save(controller.Chat, Path.Combine(output, "chat.png"));
        Save(controller.Pet, Path.Combine(output, "remi.png"));
        var settings = new SettingsWindow(controller);
        settings.Show(); await Task.Delay(150); settings.UpdateLayout();
        Save(settings, Path.Combine(output, "settings.png"));
        var tabs = settings.SettingsTabs.Items.Cast<System.Windows.Controls.TabItem>().ToArray();
        Check(tabs.All(t => Math.Abs(t.ActualWidth - tabs[0].ActualWidth) <= 1 && Math.Abs(t.ActualHeight - tabs[0].ActualHeight) <= 1 && t.ActualHeight >= 37), "settings tabs have equal dimensions (within pixel rounding): " + string.Join(", ", tabs.Select(t => $"{t.ActualWidth:F2}×{t.ActualHeight:F2}")));
        settings.CharacterBox.SelectedIndex = 2; settings.UpdateLayout();
        Save(settings, Path.Combine(output, "settings-robot.png"));
        settings.ThemeBox.SelectedIndex = 2; settings.SettingsTabs.SelectedIndex = 1; settings.ProviderBox.SelectedIndex = 1;
        settings.UpdateLayout();
        Save(settings, Path.Combine(output, "settings-openai-midnight.png"));
        Save(controller.Chat, Path.Combine(output, "chat-midnight.png"));
        Check(((SolidColorBrush)Application.Current.Resources["Paper"]).Color.ToString() == "#FF17232B", "live theme preview");
        settings.Close();
        Check(((SolidColorBrush)Application.Current.Resources["Paper"]).Color.ToString() == "#FFF8F7F2", "cancel restores saved theme");
        controller.Pet.CompanionMenu.PlacementTarget = controller.Pet.Character;
        controller.Pet.CompanionMenu.IsOpen = true; await Task.Delay(60);
        Check(controller.Pet.QuitMenuItem.IsVisible && controller.Pet.QuitMenuItem.Header.ToString() == "Close Rémi", "companion menu exposes close action");
        controller.Pet.CompanionMenu.IsOpen = false;
        var keyPath = Path.Combine(output, "key-store-test.bin");
        var keys = new Services.ProtectedApiKeyStore(keyPath);
        keys.Write("synthetic-fixture-key"); Check(keys.Read() == "synthetic-fixture-key", "Windows encrypted key roundtrip");
        Check(!Encoding.UTF8.GetString(File.ReadAllBytes(keyPath)).Contains("synthetic-fixture-key"), "key is not plaintext on disk");
        keys.Write(null); Check(!File.Exists(keyPath), "remove encrypted key");
        var savedSettings = new SettingsWindow(controller); savedSettings.Show();
        savedSettings.CharacterBox.SelectedIndex = 2; savedSettings.ThemeBox.SelectedIndex = 1;
        savedSettings.AccentBox.Text = "#6545A0"; savedSettings.ProviderBox.SelectedIndex = 1;
        savedSettings.ApiKeyBox.Password = "synthetic-fixture-key";
        savedSettings.SaveChangesButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(!savedSettings.IsVisible && controller.Settings.CharacterStyle == "robot" && controller.Settings.Theme == "slate" && controller.Settings.AccentColor == "#6545A0" && controller.Settings.Provider == "openai", "settings save collects companion, theme, and provider");
        var reopened = new SettingsWindow(controller); reopened.Show();
        Check(reopened.CharacterBox.SelectedIndex == 2 && reopened.ThemeBox.SelectedIndex == 1 && reopened.ApiKeyBox.Password.Length == 0 && controller.OpenAiKey() == "synthetic-fixture-key", "settings reopen preserves selections without exposing saved key");
        reopened.Close();
        controller.ApplySettings(controller.Settings with { CharacterStyle = "professional", Theme = "eucalyptus", AccentColor = "", Provider = "ollama" }, removeKey: true);
        // A presentation board made from the real native controls, not a separate mockup.
        var board = new DrawingVisual();
        using (var dc = board.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(232, 237, 226)), null, new Rect(0, 0, 1000, 730));
            dc.DrawImage(Render(controller.Pet), new Rect(90, 250, controller.Pet.Width, controller.Pet.Height));
            dc.DrawImage(Render(controller.Chat), new Rect(460, 65, 410, 590));
            dc.DrawText(new FormattedText("rémi", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"), 44, new SolidColorBrush(Color.FromRgb(38, 60, 52)), 1), new Point(100, 82));
            dc.DrawText(new FormattedText("A little company.\nA lighter footprint.", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 20, new SolidColorBrush(Color.FromRgb(100, 121, 102)), 1), new Point(103, 151));
        }
        WriteBitmap(board, 1000, 730, Path.Combine(output, "preview.png"));
        // Exercise visibility, keyboard shortcut registration, all moods, and retained layout.
        var handle = new System.Windows.Interop.WindowInteropHelper(controller.Pet).Handle;
        foreach (var mood in Enum.GetValues<Mood>()) { controller.Pet.Character.SetMood(mood); controller.Pet.UpdateLayout(); }
        NativeMethods.PostMessage(handle, 0x0312, new IntPtr(NativeMethods.HotkeyId), IntPtr.Zero); await Task.Delay(100);
        Check(!controller.Pet.IsVisible && !controller.Chat.IsVisible, "native shortcut message hides windows");
        NativeMethods.PostMessage(handle, 0x0312, new IntPtr(NativeMethods.HotkeyId), IntPtr.Zero); await Task.Delay(100);
        Check(controller.Pet.IsVisible, "native shortcut message restores pet");
        controller.ClearChat(); Check(controller.Messages.Count == 0, "clear conversation");
        controller.Chat.Composer.Text = "Hello from the native UI";
        controller.Chat.SendButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await WaitForIdle(controller);
        Check(controller.Messages.Count == 2 && controller.Messages[1].Content == "Hello, friend!", "send button through streamed HTTP reply");
        Check(controller.Chat.Status.Visibility == Visibility.Collapsed, "completed reply status");
        var pending = controller.SendAsync("cancel this");
        Check(controller.IsBusy, "in-flight reply"); controller.Cancel(); await pending;
        Check(!controller.IsBusy && controller.Messages.Count == 2, "cancel request without polluting context");
        await controller.SendAsync("fail this");
        Check(controller.Chat.Status.Text.Contains("fixture unavailable") && controller.Messages.Count == 2, "HTTP failure feedback");
        controller.ApplySettings(controller.Settings with { Provider = "openai" }, "synthetic-fixture-key");
        Check(controller.Messages.Count == 0, "switching providers clears previous context");
        controller.Chat.Composer.Clear(); await controller.SendAsync("Hello from OpenAI");
        Check(controller.Messages.Count == 2 && controller.Messages[1].Content == "Hello, friend!", "OpenAI SSE reply in native chat");
        Check(controller.Chat.ConnectionLabel.Text.StartsWith("OpenAI"), "OpenAI connection label");
        var openAiPending = controller.SendAsync("cancel this"); controller.Cancel(); await openAiPending;
        Check(!controller.IsBusy && controller.Messages.Count == 2, "OpenAI cancellation");
        await controller.SendAsync("fail this");
        Check(controller.Chat.Status.Text.Contains("fixture unavailable") && controller.Messages.Count == 2, "OpenAI failure feedback");
        controller.ApplySettings(controller.Settings with { Provider = "ollama" }, removeKey: true);
        Check(controller.OpenAiKey() == null, "removing saved credential");
        controller.ClearChat();
        var process = Process.GetCurrentProcess(); process.Refresh();
        File.WriteAllText(Path.Combine(output, "smoke-result.txt"), $"PASS: native windows, three companion styles, equal settings tabs, live theme preview and cancel, right-click close menu, encrypted credential storage and removal, Ollama and OpenAI streaming, provider isolation, cancellation, HTTP errors, tray and shortcut messages.\nGlobal shortcut registered: {shortcutRegistered}\nWorking set after exercising all windows and rendering screenshots: {process.WorkingSet64 / 1048576.0:F1} MiB\n");
    }

    internal static async Task MeasureIdleAsync(string[] arguments)
    {
        var output = arguments.FirstOrDefault(a => a.StartsWith("--output=", StringComparison.Ordinal))?[9..]
            ?? Path.Combine(Environment.CurrentDirectory, "artifacts", "benchmark");
        Directory.CreateDirectory(output);
        await Task.Delay(2500);
        using var process = Process.GetCurrentProcess(); process.Refresh();
        var cpuStart = process.TotalProcessorTime; var clock = Stopwatch.StartNew();
        await Task.Delay(12000); process.Refresh();
        var result = new
        {
            measuredAtUtc = DateTime.UtcNow,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            operatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            scenario = "Pet visible, tray and shortcut registered, chat closed, blinking enabled, no model loaded by Remi",
            durationSeconds = Math.Round(clock.Elapsed.TotalSeconds, 2),
            cpuPercentOfOneCore = Math.Round((process.TotalProcessorTime - cpuStart).TotalMilliseconds / clock.Elapsed.TotalMilliseconds * 100, 3),
            workingSetMiB = Math.Round(process.WorkingSet64 / 1048576.0, 1),
            privateMemoryMiB = Math.Round(process.PrivateMemorySize64 / 1048576.0, 1)
        };
        File.WriteAllText(Path.Combine(output, "idle.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task WaitForIdle(CompanionController controller)
    {
        var wait = Stopwatch.StartNew();
        while (controller.IsBusy && wait.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(20);
        Check(!controller.IsBusy, "operation completes");
    }

    internal sealed class OllamaFixture : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var openAi = request.RequestUri!.Host == "api.openai.com";
            if (openAi) Check(request.Headers.Authorization?.Parameter == "synthetic-fixture-key", "OpenAI fixture credential");
            else Check(request.Headers.Authorization == null, "Ollama never receives API key");
            if (openAi && request.Method == HttpMethod.Get) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[{\"id\":\"gpt-4.1-mini\"}]}") };
            if (request.Method == HttpMethod.Get) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"models\":[{\"name\":\"llama3.2\"}]}") };
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var last = json.RootElement.GetProperty(openAi ? "input" : "messages").EnumerateArray().Last().GetProperty("content").GetString();
            if (last == "cancel this") await Task.Delay(5000, token);
            if (last == "fail this") return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent(openAi ? "{\"error\":{\"message\":\"fixture unavailable\"}}" : "{\"error\":\"fixture unavailable\"}") };
            if (openAi) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("data: {\"type\":\"response.output_text.delta\",\"delta\":\"Hello, \"}\n\ndata: {\"type\":\"response.output_text.delta\",\"delta\":\"friend!\"}\n\ndata: {\"type\":\"response.completed\"}\n\n", Encoding.UTF8, "text/event-stream") };
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"message\":{\"content\":\"Hello, \"},\"done\":false}\n{\"message\":{\"content\":\"friend!\"},\"done\":true}\n", Encoding.UTF8, "application/x-ndjson") };
        }
    }

    private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException("Smoke test failed: " + name); }
    private static RenderTargetBitmap Render(FrameworkElement element)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element); return bitmap;
    }
    private static void Save(FrameworkElement element, string path) => Encode(Render(element), path);
    private static void WriteBitmap(Visual visual, int width, int height, string path)
    { var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); Encode(bitmap, path); }
    private static void Encode(BitmapSource bitmap, string path)
    { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file); }
}
