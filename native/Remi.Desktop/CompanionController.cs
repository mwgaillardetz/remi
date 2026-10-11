using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Remi.Core;
using Remi.Desktop.Character;
using Remi.Desktop.Services;

namespace Remi.Desktop;

public sealed class MessageViewModel(string role, string content) : INotifyPropertyChanged
{
    public string Role { get; } = role;
    private string content = content;
    public string Content { get => content; set { content = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class CompanionController : IDisposable
{
    private readonly SettingsStore store;
    private readonly bool preview;
    private readonly HttpClient http;
    private readonly OllamaClient ollama;
    private readonly OpenAiClient openAi;
    private readonly IApiKeyStore apiKeys;
    private CancellationTokenSource? operation;
    private string[]? models;
    private MicrophoneRecording? recording;
    private readonly DispatcherTimer recordingLimit = new() { Interval = TimeSpan.FromSeconds(45) };
    private readonly DispatcherTimer mouthReset = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private bool disposed;
    private ChatWindow? chat;
    private SettingsWindow? settingsWindow;
    public Settings Settings { get; private set; }
    public PetWindow Pet { get; }
    public ChatWindow Chat
    {
        get
        {
            if (chat == null)
            {
                chat = new ChatWindow(this); chat.RefreshConnection();
                if (store.LoadWarning != null) chat.SetStatus(store.LoadWarning, true);
            }
            return chat;
        }
    }
    public VoiceService Voice { get; } = new();
    public ObservableCollection<MessageViewModel> Messages { get; } = [];
    public bool IsBusy => operation != null;

    public CompanionController(SettingsStore store, bool preview = false, HttpMessageHandler? handler = null, IApiKeyStore? apiKeys = null)
    {
        this.store = store; this.preview = preview;
        http = handler == null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = Timeout.InfiniteTimeSpan;
        Settings = preview ? new Settings { SpeechEnabled = false } : store.Load();
        this.apiKeys = apiKeys ?? (preview ? new MemoryApiKeyStore() : new ProtectedApiKeyStore(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Remi", "openai-key.bin")));
        ollama = new OllamaClient(http);
        openAi = new OpenAiClient(http);
        ThemeManager.Apply(Settings);
        Pet = new PetWindow(this);
        recordingLimit.Tick += async (_, _) => { recordingLimit.Stop(); await ToggleRecordingAsync(); };
        mouthReset.Tick += (_, _) => { mouthReset.Stop(); Pet.Character.Articulate(false); };
        Voice.SpeakingChanged += speaking => Application.Current.Dispatcher.BeginInvoke(() =>
        { if (!disposed && !IsBusy) SetMood(speaking ? Mood.Talking : Mood.Idle); });
        Voice.WordSpoken += () => Application.Current.Dispatcher.BeginInvoke(() =>
        { if (!disposed) { Pet.Character.Articulate(true); mouthReset.Stop(); mouthReset.Start(); } });
    }

    public void ToggleChat()
    {
        if (chat?.IsVisible == true) { CloseChat(); return; }
        ShowChat();
    }
    public void ShowChat() { if (!Pet.IsVisible) Pet.Show(); var window = Chat; PositionChat(); window.Show(); window.Activate(); }
    public void CloseChat() { Cancel(); chat?.Hide(); }
    public void ToggleVisibility()
    {
        if (Pet.IsVisible) { Pet.Hide(); chat?.Hide(); settingsWindow?.Hide(); Cancel(); Voice.Stop(); }
        else { Pet.Show(); NativeMethods.KeepOnScreen(Pet); }
    }
    public void PositionChat()
    {
        if (chat == null) return;
        var area = NativeMethods.WorkArea(Pet);
        Chat.Left = Pet.Left - Chat.Width - 12;
        if (Chat.Left < area.Left) Chat.Left = Pet.Left + Pet.Width + 12;
        Chat.Top = Pet.Top + Pet.Height - Chat.Height;
        // Clamp against the pet's monitor even before the chat HWND has moved there.
        Chat.Left = Math.Clamp(Chat.Left, area.Left, Math.Max(area.Left, area.Right - Chat.Width));
        Chat.Top = Math.Clamp(Chat.Top, area.Top, Math.Max(area.Top, area.Bottom - Chat.Height));
    }
    public void SavePosition()
    {
        if (preview || store.LoadWarning != null) return;
        try { Settings = Settings with { Left = Pet.Left, Top = Pet.Top }; store.Save(Settings); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { Chat.SetStatus("Could not save Rémi's position: " + ex.Message, true); }
    }
    public string? OpenAiKey() => apiKeys.Read() ?? (preview ? null : Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    public void ApplySettings(Settings value, string? apiKey = null, bool removeKey = false)
    {
        if (IsBusy) throw new InvalidOperationException("Finish or stop the current conversation before saving settings.");
        value = value.Normalize();
        if (apiKey != null && apiKey.Any(char.IsWhiteSpace)) throw new ArgumentException("The API key must not contain whitespace.");
        if (removeKey || !string.IsNullOrEmpty(apiKey)) apiKeys.Write(removeKey ? null : apiKey);
        if (!preview) store.Save(value);
        if (Settings.Host != value.Host) models = null;
        if (Settings.Provider != value.Provider) { Messages.Clear(); chat?.SetStatus(null); }
        Settings = value; ThemeManager.Apply(value); Pet.Configure(value); chat?.RefreshConnection(); chat?.RefreshMessages(); PositionChat();
        if (!value.SpeechEnabled) Voice.Stop();
    }
    public async Task<string[]> RefreshModelsAsync(string host, CancellationToken token)
    {
        var list = (await ollama.ListModelsAsync(host, token)).ToArray();
        if (host == Settings.Host) models = list;
        return list;
    }
    public async Task<string[]> RefreshOpenAiModelsAsync(string? key, CancellationToken token) =>
        (await openAi.ListModelsAsync(key ?? OpenAiKey() ?? "", token)).ToArray();
    public void OpenSettings()
    {
        if (settingsWindow == null)
        {
            settingsWindow = new SettingsWindow(this);
            settingsWindow.Closed += (_, _) => settingsWindow = null;
        }
        settingsWindow.Show(); settingsWindow.Activate();
    }
    private void SetMood(Mood mood)
    {
        Pet.Character.SetMood(mood);
    }

    public async Task SendAsync(string text, Mood mood = Mood.Thinking)
    {
        if (IsBusy || disposed || string.IsNullOrWhiteSpace(text)) return;
        Voice.Stop();
        using var source = new CancellationTokenSource(); operation = source;
        var snapshot = Settings;
        var user = new MessageViewModel("user", text);
        Messages.Add(user);
        // Bound both UI retention and the actual model context. Conversations are memory-only.
        while (Messages.Count > 100) Messages.RemoveAt(0);
        var history = Messages.Select(m => new ChatMessage(m.Role, m.Content)).ToArray();
        MessageViewModel? reply = null;
        var response = new StringBuilder();
        var flush = Stopwatch.StartNew();
        var completed = false;
        Chat.SetBusy(true); Chat.SetStatus("Rémi is thinking…"); SetMood(mood);
        try
        {
            if (snapshot.Provider == "ollama")
            {
            if (models == null) models = (await ollama.ListModelsAsync(snapshot.Host, source.Token)).ToArray();
            if (models.Length == 0) throw new InvalidOperationException("No Ollama models are installed. Run: ollama pull llama3.2, then refresh models in settings.");
            var model = models.FirstOrDefault(m => m == snapshot.Model) ?? models.FirstOrDefault(m => m.StartsWith(snapshot.Model + ':', StringComparison.Ordinal)) ?? models[0];
            snapshot = snapshot with { Model = model };
            if (Settings.Model != model) { Settings = Settings with { Model = model }; Chat.RefreshConnection(); }
            }
            reply = new MessageViewModel("assistant", "…"); Messages.Add(reply);
            var stream = snapshot.Provider == "openai" ? openAi.ChatAsync(snapshot, OpenAiKey() ?? "", history, source.Token) : ollama.ChatAsync(snapshot, history, source.Token);
            await foreach (var chunk in stream)
            {
                response.Append(chunk);
                // Coalesce fast token streams so layout runs at most 20 times per second.
                if (flush.ElapsedMilliseconds < 50) continue;
                reply.Content = response.ToString(); Chat.RefreshMessages(); flush.Restart();
            }
            reply.Content = response.ToString();
            if (response.Length == 0) throw new InvalidOperationException("The provider returned an empty reply. Try another model in settings.");
            completed = true; Chat.SetStatus(null);
        }
        catch (OperationCanceledException)
        { Chat.SetStatus(source.IsCancellationRequested ? "Stopped. Take your time." : "The provider took too long. Check your connection or try another model."); }
        catch (Exception ex) when (ex is HttpRequestException or System.IO.IOException or System.Text.Json.JsonException or InvalidOperationException or ArgumentException)
        { models = null; Chat.SetStatus(ex is HttpRequestException { StatusCode: null } ? (snapshot.Provider == "openai" ? "Could not reach OpenAI. Check your internet connection." : "Could not reach Ollama. Start Ollama or check the host in settings.") : ex.Message, true); }
        catch (System.Security.Cryptography.CryptographicException)
        { Chat.SetStatus("Could not unlock the saved API key. Replace it in Settings → Connection.", true); }
        finally
        {
            if (reply != null)
            {
                if (response.Length == 0) Messages.Remove(reply);
                else reply.Content = response.ToString();
            }
            if (!completed) Messages.Remove(user); // Failed requests never poison the next model context.
            if (!completed && reply != null) Messages.Remove(reply);
            if (!completed && Chat.Composer.Text.Length == 0) Chat.Composer.Text = text;
            operation = null;
            if (!disposed) { Chat.SetBusy(false); SetMood(Mood.Idle); Chat.RefreshMessages(); }
        }
        if (completed && !disposed)
        {
            try { Voice.Speak(response.ToString(), Settings); }
            catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
            { Chat.SetStatus("The reply is ready, but speech is unavailable. " + ex.Message, true); }
        }
    }

    public async Task ToggleRecordingAsync()
    {
        if (disposed) return;
        if (recording == null)
        {
            if (IsBusy) return;
            Voice.Stop();
            try
            {
                recording = new MicrophoneRecording(Settings.Microphone);
                operation = new CancellationTokenSource();
                recordingLimit.Start(); Chat.SetBusy(true, true); Chat.SetStatus("Listening… click the microphone to finish (45 second limit)."); SetMood(Mood.Listening);
            }
            catch (Exception ex) { recording?.Dispose(); recording = null; Chat.SetStatus("Microphone unavailable. Check Windows microphone privacy settings. " + ex.Message, true); }
            return;
        }
        recordingLimit.Stop();
        var current = recording; recording = null;
        var source = operation!;
        string? transcript = null;
        Chat.SetBusy(true); Chat.SetStatus("Transcribing on your device…"); SetMood(Mood.Thinking);
        try
        {
            await current.StopAsync(source.Token);
            transcript = await current.TranscribeAsync(Settings, source.Token);
            if (string.IsNullOrWhiteSpace(transcript)) Chat.SetStatus("I didn't catch any speech. Try again, or type a message.");
        }
        catch (OperationCanceledException) { Chat.SetStatus("Voice input stopped."); }
        catch (Exception ex) { Chat.SetStatus(ex.Message, true); }
        finally
        {
            current.Dispose(); source.Dispose(); operation = null;
            if (!disposed) { Chat.SetBusy(false); SetMood(Mood.Idle); }
        }
        if (!disposed && !string.IsNullOrWhiteSpace(transcript)) await SendAsync(transcript);
    }

    public void Cancel()
    {
        operation?.Cancel(); Voice.Stop();
        if (recording != null)
        {
            recordingLimit.Stop(); recording.Dispose(); recording = null;
            operation?.Dispose(); operation = null;
            Chat.SetBusy(false); Chat.SetStatus("Recording discarded."); SetMood(Mood.Idle);
        }
    }
    public void ClearChat() { Cancel(); Messages.Clear(); Chat.SetStatus(null); }
    public void Dispose()
    {
        if (disposed) return;
        SavePosition(); disposed = true; Cancel();
        recordingLimit.Stop(); mouthReset.Stop(); Voice.Dispose(); http.Dispose();
        settingsWindow?.Close(); chat?.Close(); Pet.Close();
    }
}
