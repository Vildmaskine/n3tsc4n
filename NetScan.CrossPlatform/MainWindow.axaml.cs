using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NetScan.CrossPlatform.ViewModels;

namespace NetScan.CrossPlatform;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
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

    protected override void OnWindowStateChanged(WindowState state)
    {
        base.OnWindowStateChanged(state);
        UpdateMaximizeIcon();
    }

    private void UpdateMaximizeIcon()
    {
        if (MaximizeIcon == null) return;
        // Restore icon when maximized, maximize icon when normal
        MaximizeIcon.Data = Avalonia.Media.Geometry.Parse(
            WindowState == WindowState.Maximized
                ? "M 3,0 L 11,0 L 11,8 L 3,8 Z M 0,3 L 8,3 L 8,11 L 0,11 Z"
                : "M 1,1 L 11,1 L 11,11 L 1,11 Z");
    }

    private void DGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedResult is { HasWeb: true } r)
            MainViewModel.OpenUrl(r.WebUrl);
    }
}
