using System;
using System.Windows;
using System.Windows.Input;
using Remi.Core;

namespace Remi.Desktop;

public partial class PetWindow : Window
{
    private readonly CompanionController controller;
    private Point? dragOrigin;
    private bool dragged;
    public PetWindow(CompanionController controller)
    {
        InitializeComponent(); this.controller = controller;
        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(this);
        Configure(controller.Settings);
        Loaded += (_, _) =>
        {
            var area = NativeMethods.WorkArea(this);
            Left = controller.Settings.Left ?? area.Right - Width - 32;
            Top = controller.Settings.Top ?? area.Bottom - Height - 24;
            NativeMethods.KeepOnScreen(this);
        };
    }

    public void Configure(Settings settings)
    {
        Width = 280 * settings.Scale / 100;
        Height = 292 * settings.Scale / 100;
        Character.Configure(settings);
        if (IsLoaded) NativeMethods.KeepOnScreen(this);
    }

    private void PetDown(object sender, MouseButtonEventArgs e)
    { Character.Focus(); dragOrigin = e.GetPosition(this); dragged = false; Character.CaptureMouse(); }

    private void PetMove(object sender, MouseEventArgs e)
    {
        if (dragOrigin is not { } origin || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(this);
        if (Math.Abs(point.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(point.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        dragged = true; dragOrigin = null; Character.ReleaseMouseCapture();
        try { DragMove(); } catch (InvalidOperationException) { }
        NativeMethods.KeepOnScreen(this); controller.SavePosition(); controller.PositionChat();
    }

    private void PetUp(object sender, MouseButtonEventArgs e)
    { Character.ReleaseMouseCapture(); if (!dragged && dragOrigin != null) controller.ToggleChat(); dragOrigin = null; }
    private void PetKeyDown(object sender, KeyEventArgs e)
    { if (e.Key is Key.Enter or Key.Space) { controller.ToggleChat(); e.Handled = true; } }
    private void ChatClick(object sender, RoutedEventArgs e) => controller.ShowChat();
    private void SettingsClick(object sender, RoutedEventArgs e) => controller.OpenSettings();
    private void HideClick(object sender, RoutedEventArgs e) => controller.ToggleVisibility();
    private void QuitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
