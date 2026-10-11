using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Remi.Core;

namespace Remi.Desktop.Services;

public sealed record MicrophoneDevice(int Id, string Name);

public sealed class VoiceService : IDisposable
{
    private SpeechSynthesizer? synthesizer;
    public event Action<bool>? SpeakingChanged;
    public event Action? WordSpoken;

    private SpeechSynthesizer Synthesizer
    {
        get
        {
            if (synthesizer != null) return synthesizer;
            synthesizer = new SpeechSynthesizer();
            synthesizer.SpeakStarted += (_, _) => SpeakingChanged?.Invoke(true);
            synthesizer.SpeakCompleted += (_, _) => SpeakingChanged?.Invoke(false);
            synthesizer.SpeakProgress += (_, _) => WordSpoken?.Invoke();
            return synthesizer;
        }
    }

    public string[] Voices() => Synthesizer.GetInstalledVoices().Where(v => v.Enabled).Select(v => v.VoiceInfo.Name).ToArray();
    public static IReadOnlyList<MicrophoneDevice> Microphones()
    {
        var result = new List<MicrophoneDevice> { new(-1, "Windows default microphone") };
        for (var i = 0; i < WaveInEvent.DeviceCount; i++) result.Add(new(i, WaveInEvent.GetCapabilities(i).ProductName));
        return result;
    }

    public void Speak(string text, Settings settings)
    {
        Stop();
        if (!settings.SpeechEnabled) return;
        var clean = Regex.Replace(text, @"[*_~`#>|\\]", "").Trim();
        if (clean.Length == 0) return;
        var synth = Synthesizer;
        synth.Rate = settings.SpeechRate;
        if (Voices().Contains(settings.Voice)) synth.SelectVoice(settings.Voice);
        else synth.SelectVoiceByHints(VoiceGender.NotSet);
        synth.SpeakAsync(clean);
    }

    public void Stop() => synthesizer?.SpeakAsyncCancelAll();
    public void Dispose() { synthesizer?.SpeakAsyncCancelAll(); synthesizer?.Dispose(); }
}

/// <summary>One explicitly requested recording. PCM goes straight to disk, not base64 IPC.</summary>
public sealed class MicrophoneRecording : IDisposable
{
    private readonly WaveInEvent input;
    private readonly WaveFileWriter writer;
    private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object sync = new();
    private bool disposed;
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "remi-" + Guid.NewGuid().ToString("N"));
    public string AudioPath => Path.Combine(DirectoryPath, "voice.wav");

    public MicrophoneRecording(int device)
    {
        Directory.CreateDirectory(DirectoryPath);
        input = new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
        writer = new WaveFileWriter(AudioPath, input.WaveFormat);
        input.DataAvailable += (_, e) => { lock (sync) { if (!disposed) writer.Write(e.Buffer, 0, e.BytesRecorded); } };
        input.RecordingStopped += (_, e) =>
        {
            lock (sync) { if (!disposed) writer.Flush(); }
            if (e.Exception != null) stopped.TrySetException(e.Exception);
            else stopped.TrySetResult();
        };
        try { input.StartRecording(); }
        catch { Dispose(); throw; }
    }

    public async Task StopAsync(CancellationToken token)
    {
        input.StopRecording();
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        lock (sync) { if (!disposed) { writer.Dispose(); disposed = true; } }
        input.Dispose();
    }

    public async Task<string> TranscribeAsync(Settings settings, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(settings.PythonPath)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true }
        };
        foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "Voice", "transcribe.py"), "--audio", AudioPath,
            "--model", settings.WhisperModel, "--output", Path.ChangeExtension(AudioPath, ".txt") })
            process.StartInfo.ArgumentList.Add(argument);
        try
        {
            process.Start();
            using var registration = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { } });
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(timeout.Token);
            await stdout;
            var errors = await stderr;
            var transcriptPath = Path.ChangeExtension(AudioPath, ".txt");
            if (process.ExitCode != 0 || !File.Exists(transcriptPath))
            {
                if (errors.Contains("No module named") && errors.Contains("whisper"))
                    throw new InvalidOperationException("Install local voice input with: python -m pip install openai-whisper. You can still type a message.");
                throw new InvalidOperationException("Whisper could not transcribe this recording. Check the Python path in settings. " + errors[..Math.Min(errors.Length, 240)]);
            }
            return (await File.ReadAllTextAsync(transcriptPath, timeout.Token)).Trim();
        }
        catch (System.ComponentModel.Win32Exception ex)
        { throw new InvalidOperationException("Python was not found. Install Python and openai-whisper, or set its full path in settings.", ex); }
        finally
        {
            // Wait for a cancelled worker to release files before the recording is disposed.
            try { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
    }

    public void Dispose()
    {
        input.Dispose();
        lock (sync) { if (!disposed) { disposed = true; writer.Dispose(); } }
        try { Directory.Delete(DirectoryPath, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
