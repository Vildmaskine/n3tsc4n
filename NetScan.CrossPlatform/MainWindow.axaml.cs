using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using NetScan.CrossPlatform.ViewModels;

namespace NetScan.CrossPlatform;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        // X11 window managers often ignore ExtendClientAreaChromeHints.NoChrome,
        // causing a double title bar. BorderOnly removes the system titlebar reliably
        // on Linux and macOS while keeping the resize border.
        if (!OperatingSystem.IsWindows())
            SystemDecorations = SystemDecorations.BorderOnly;

        try
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://NetScan/app.ico")));
        }
        catch { }

        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionLabel.Text = $"v{ver?.Major}.{ver?.Minor}.{ver?.Build}";

        this.PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
                UpdateMaximizeIcon();
        };
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void BtnMinimize_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        UpdateMaximizeIcon();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e) =>
        Close();

    private void UpdateMaximizeIcon()
    {
        if (MaximizeIcon == null) return;
        MaximizeIcon.Data = Avalonia.Media.Geometry.Parse(
            WindowState == WindowState.Maximized
                ? "M 2,0 L 10,0 L 10,8 L 2,8 Z M 0,2 L 8,2 L 8,10 L 0,10 Z"
                : "M 0,0 L 10,0 L 10,10 L 0,10 Z");
    }

    private void DGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedResult is { HasWeb: true } r)
            MainViewModel.OpenUrl(r.WebUrl);
    }
}
