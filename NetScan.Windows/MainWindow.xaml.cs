using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using NetScan.Core;

namespace NetScan.Windows;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ScanResult> _results = new();
    private readonly OuiLookup _oui = new();
    private readonly string _defaultSubnet;
    private CancellationTokenSource? _cts;
    private DateTime _lastScannedAt;

    private static readonly Regex SubnetRegex =
        new(@"^\d{1,3}\.\d{1,3}\.\d{1,3}$", RegexOptions.Compiled);

    public MainWindow()
    {
        InitializeComponent();

        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionLabel.Text = $"v{ver?.Major}.{ver?.Minor}.{ver?.Build}";

        // Detect and set default subnet
        _defaultSubnet = ScanEngine.GetDefaultSubnet();
        SubnetBox.Text = _defaultSubnet;
        SubnetBox.TextChanged += (_, _) =>
            SubnetWarning.Visibility = SubnetBox.Text.Trim() != _defaultSubnet
                ? Visibility.Visible
                : Visibility.Collapsed;

        // Bind DataGrid with numeric IP sorting
        var view = CollectionViewSource.GetDefaultView(_results);
        view.SortDescriptions.Add(new SortDescription(nameof(ScanResult.IpSortKey), ListSortDirection.Ascending));
        DGrid.ItemsSource = view;

        // Wire custom scrollbar to DataGrid's internal ScrollViewer
        DGrid.Loaded += (_, _) =>
        {
            var sv = FindVisualChild<System.Windows.Controls.ScrollViewer>(DGrid);
            if (sv == null) return;
            sv.ScrollChanged += (_, _) =>
            {
                DGridVScrollBar.Maximum      = sv.ScrollableHeight;
                DGridVScrollBar.ViewportSize = sv.ViewportHeight;
                DGridVScrollBar.Value        = sv.VerticalOffset;
                DGridVScrollBar.Visibility   = sv.ScrollableHeight > 0 ? Visibility.Visible : Visibility.Collapsed;
            };
            DGridVScrollBar.ValueChanged += (_, e) => sv.ScrollToVerticalOffset(e.NewValue);
        };

        // Initialize OUI database in background
        InitializeOuiAsync();
    }

    private async void InitializeOuiAsync()
    {
        var progress = new Progress<string>(msg => StatusText.Text = msg);
        try
        {
            await _oui.InitializeAsync(progress);
        }
        catch
        {
            StatusText.Text = "Ready (OUI unavailable)";
        }
    }

    private async void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        var subnet = SubnetBox.Text.Trim();
        if (!SubnetRegex.IsMatch(subnet))
        {
            MessageBox.Show(
                "Enter a valid subnet, e.g. 192.168.1",
                "Invalid subnet",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Reset UI state
        _results.Clear();
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _lastScannedAt = DateTime.Now;

        BtnScan.IsEnabled   = false;
        BtnStop.IsEnabled   = true;
        BtnWeb.IsEnabled    = false;
        BtnRDP.IsEnabled    = false;
        BtnSMB.IsEnabled    = false;
        BtnCopy.IsEnabled   = false;
        BtnExport.IsEnabled = false;

        // Phase 1: ping sweep — determinate progress
        PBar.IsIndeterminate = false;
        PBar.Maximum = 254;
        PBar.Value   = 0;
        StatusText.Text = $"Scanning {subnet}.0/24...";

        var progress = new Progress<int>(v => PBar.Value = v);

        var engine = new ScanEngine(_oui);
        int found = 0;

        try
        {
            await Task.Run(async () =>
            {
                await foreach (var result in engine.ScanAsync(subnet, progress, ct))
                {
                    found++;
                    var captured = result;
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _results.Add(captured);
                        StatusText.Text = $"Found: {found} device(s)...";

                        if (PBar.Value >= 254 && !PBar.IsIndeterminate)
                            PBar.IsIndeterminate = true;
                    });
                }
            }, ct);

            await Dispatcher.InvokeAsync(() =>
            {
                PBar.IsIndeterminate = false;
                PBar.Value = PBar.Maximum;
                StatusText.Text     = $"Done — {found} device(s)";
                BtnExport.IsEnabled = found > 0;
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                PBar.IsIndeterminate = false;
                PBar.Value = 0;
                StatusText.Text     = "Stopped";
                BtnExport.IsEnabled = _results.Count > 0;
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                PBar.IsIndeterminate = false;
                PBar.Value = 0;
                StatusText.Text = $"Error: {ex.Message}";
            });
        }
        finally
        {
            await Dispatcher.InvokeAsync(() =>
            {
                BtnScan.IsEnabled = true;
                BtnStop.IsEnabled = false;
            });
        }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        BtnStop.IsEnabled = false;
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void BtnClose_Click(object sender, RoutedEventArgs e) =>
        Close();

    private void Window_StateChanged(object sender, EventArgs e)
    {
        MaximizeIcon.Data = System.Windows.Media.Geometry.Parse(
            WindowState == WindowState.Maximized
                ? "M 3,0 L 12,0 L 12,9 L 3,9 Z M 0,3 L 9,3 L 9,12 L 0,12 Z"
                : "M 0,0 L 12,0 L 12,12 L 0,12 Z");
    }

    private void DGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var selected = DGrid.SelectedItem as ScanResult;
        BtnCopy.IsEnabled = selected != null;
        BtnWeb.IsEnabled  = selected?.HasWeb == true;
        BtnRDP.IsEnabled  = selected?.HasRDP == true;
        BtnSMB.IsEnabled  = selected?.HasSMB == true;
    }

    private void BtnWeb_Click(object sender, RoutedEventArgs e)
    {
        if (DGrid.SelectedItem is ScanResult r && r.HasWeb)
            OpenUrl(r.WebUrl);
    }

    private void BtnRDP_Click(object sender, RoutedEventArgs e)
    {
        if (DGrid.SelectedItem is ScanResult r)
        {
            try
            {
                Process.Start("mstsc", $"/v:{r.IP}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not start RDP: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnSMB_Click(object sender, RoutedEventArgs e)
    {
        if (DGrid.SelectedItem is ScanResult r)
        {
            try
            {
                Process.Start(new ProcessStartInfo($@"\\{r.IP}") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open SMB share: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (DGrid.SelectedItem is ScanResult r)
        {
            Clipboard.SetText(r.IP);
            StatusText.Text = "Copied!";
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_results.Count == 0) return;

        var dlg = new SaveFileDialog
        {
            Title    = "Export scan",
            Filter   = "HTML file|*.html",
            FileName = $"N3tSc4n_{SubnetBox.Text.Trim()}.html"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var html = HtmlExporter.Generate(_results, SubnetBox.Text.Trim(), _lastScannedAt);
            File.WriteAllText(dlg.FileName, html, System.Text.Encoding.UTF8);
            StatusText.Text = "Exported!";
            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = $"Export failed: {ex.Message}";
        }
    }

    private void DGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DGrid.SelectedItem is ScanResult r && r.HasWeb)
            OpenUrl(r.WebUrl);
    }

    private static T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T hit) return hit;
            var found = FindVisualChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            var psi = new ProcessStartInfo(url) { UseShellExecute = true };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open browser: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
