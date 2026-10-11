using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using Remi.Core;
using Forms = System.Windows.Forms;

namespace Remi.Desktop;

public partial class App : Application
{
    private CompanionController? controller;
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? icon;
    private Mutex? singleton;
    private EventWaitHandle? activation;
    private RegisteredWaitHandle? activationWait;
    private HwndSource? source;
    private bool ownsMutex;
    private bool shortcutRegistered;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var smoke = Array.IndexOf(e.Args, "--smoke-test") >= 0;
        var benchmark = Array.IndexOf(e.Args, "--benchmark") >= 0;
        var suffix = Environment.UserName;
        if (!smoke && !benchmark)
        {
            singleton = new Mutex(true, @"Local\Remi.Native." + suffix, out ownsMutex);
            activation = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Remi.Activate." + suffix);
            if (!ownsMutex) { activation.Set(); Shutdown(); return; }
        }
        var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Remi", "settings.json");
        try
        {
            controller = new CompanionController(new SettingsStore(settingsPath), smoke || benchmark, smoke ? new SmokeTest.OllamaFixture() : null);
            MainWindow = controller.Pet; controller.Pet.Show();
            source = HwndSource.FromHwnd(new WindowInteropHelper(controller.Pet).Handle);
            source?.AddHook(WindowMessage);
            shortcutRegistered = NativeMethods.RegisterHotKey(source!.Handle, NativeMethods.HotkeyId, 0x4006, 0x42); // Ctrl+Shift+B, no repeat.
            if (!shortcutRegistered) controller.Chat.SetStatus("Ctrl+Shift+B is used by another app. You can still use Rémi's tray icon to show or hide him.", true);
            var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/remi.ico"))!.Stream;
            using (iconStream) icon = new System.Drawing.Icon(iconStream);
            tray = new Forms.NotifyIcon { Text = "Rémi · a little company", Icon = icon, Visible = true };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Chat with Rémi", null, (_, _) => controller.ShowChat());
            menu.Items.Add("Show / hide Rémi   Ctrl+Shift+B", null, (_, _) => controller.ToggleVisibility());
            menu.Items.Add("Settings", null, (_, _) => controller.OpenSettings());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Quit Rémi", null, (_, _) => Shutdown());
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (_, args) => { if (args.Button == Forms.MouseButtons.Left) controller.ToggleVisibility(); };
            if (smoke)
            {
                await SmokeTest.RunAsync(controller, e.Args, shortcutRegistered);
                // Exercise the same close action offered by the companion's right-click menu.
                controller.Pet.QuitMenuItem.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
                return;
            }
            if (benchmark) { await SmokeTest.MeasureIdleAsync(e.Args); Shutdown(); return; }
            activationWait = ThreadPool.RegisterWaitForSingleObject(activation!, (_, _) => Dispatcher.BeginInvoke(() => controller.ShowChat()), null, Timeout.Infinite, false);
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(settingsPath)!, "startup-error.log"), ex.ToString());
            if (!smoke && !benchmark) MessageBox.Show("Rémi could not start. " + ex.Message, "Rémi", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    { if (message == 0x0312 && wParam.ToInt32() == NativeMethods.HotkeyId) { controller?.ToggleVisibility(); handled = true; } return IntPtr.Zero; }

    protected override void OnExit(ExitEventArgs e)
    {
        activationWait?.Unregister(null);
        if (shortcutRegistered && source != null) NativeMethods.UnregisterHotKey(source.Handle, NativeMethods.HotkeyId);
        source?.RemoveHook(WindowMessage);
        if (tray != null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        icon?.Dispose(); controller?.Dispose();
        activation?.Dispose();
        if (ownsMutex) singleton?.ReleaseMutex();
        singleton?.Dispose();
        base.OnExit(e);
    }
}
