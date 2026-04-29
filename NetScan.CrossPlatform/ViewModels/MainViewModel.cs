using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetScan.Core;

namespace NetScan.CrossPlatform.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly OuiLookup _oui = new();
    private CancellationTokenSource? _cts;

    private static readonly Regex SubnetRegex =
        new(@"^\d{1,3}\.\d{1,3}\.\d{1,3}$", RegexOptions.Compiled);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSubnetWarning))]
    private string _subnetText = string.Empty;

    [ObservableProperty]
    private string _defaultSubnet = string.Empty;

    [ObservableProperty]
    private string _statusText = "Klar";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private double _progressMaximum = 254;

    [ObservableProperty]
    private bool _isProgressIndeterminate;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportHtmlCommand))]
    private bool _isScanning;

    private string _lastScannedSubnet = string.Empty;
    private DateTime _lastScannedAt;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenWebCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenRdpCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenSmbCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyIpCommand))]
    private ScanResult? _selectedResult;

    public bool ShowSubnetWarning =>
        !string.IsNullOrEmpty(DefaultSubnet) && SubnetText.Trim() != DefaultSubnet;

    partial void OnSubnetTextChanged(string value) =>
        OnPropertyChanged(nameof(ShowSubnetWarning));

    public ObservableCollection<ScanResult> Results { get; } = new();

    public MainViewModel()
    {
        DefaultSubnet = ScanEngine.GetDefaultSubnet();
        SubnetText = DefaultSubnet;
        _ = InitializeOuiAsync();
        Results.CollectionChanged += (_, _) => ExportHtmlCommand.NotifyCanExecuteChanged();
    }

    private async Task InitializeOuiAsync()
    {
        var progress = new Progress<string>(msg => StatusText = msg);
        try
        {
            await _oui.InitializeAsync(progress);
        }
        catch
        {
            StatusText = "Ready (OUI unavailable)";
        }
    }

    private bool CanScan()    => !IsScanning;
    private bool CanExport()  => !IsScanning && Results.Count > 0;
    private bool CanOpenSmb() => SelectedResult?.HasSMB == true;
    private bool CanStop() => IsScanning;
    private bool CanOpenWeb() => SelectedResult?.HasWeb == true;
    private bool CanOpenRdp() => SelectedResult?.HasRDP == true;
    private bool CanCopyIp() => SelectedResult != null;

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        var subnet = SubnetText.Trim();
        if (!SubnetRegex.IsMatch(subnet))
        {
            StatusText = "Invalid subnet!";
            return;
        }

        Results.Clear();
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _lastScannedSubnet = subnet;
        _lastScannedAt = DateTime.Now;
        IsScanning = true;
        IsProgressIndeterminate = false;
        ProgressMaximum = 254;
        ProgressValue = 0;
        StatusText = $"Scanning {subnet}.0/24...";

        // Progress<int> captures the UI SynchronizationContext here (on UI thread)
        var progress = new Progress<int>(v => ProgressValue = v);

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
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        InsertSorted(captured);
                        StatusText = $"Found: {found} device(s)...";

                        if (ProgressValue >= 254 && !IsProgressIndeterminate)
                            IsProgressIndeterminate = true;
                    });
                }
            }, ct);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsProgressIndeterminate = false;
                ProgressValue = ProgressMaximum;
                StatusText = $"Done — {found} device(s)";
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsProgressIndeterminate = false;
                ProgressValue = 0;
                StatusText = "Stopped";
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsProgressIndeterminate = false;
                ProgressValue = 0;
                StatusText = $"Error: {ex.Message}";
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsScanning = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanOpenWeb))]
    private void OpenWeb()
    {
        if (SelectedResult is { HasWeb: true } r)
            OpenUrl(r.WebUrl);
    }

    [RelayCommand(CanExecute = nameof(CanOpenSmb))]
    private void OpenSmb()
    {
        if (SelectedResult is not { } r) return;
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo($@"\\{r.IP}") { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start(new ProcessStartInfo("open", $"smb://{r.IP}") { UseShellExecute = false });
            else
                Process.Start(new ProcessStartInfo("xdg-open", $"smb://{r.IP}") { UseShellExecute = false });
        }
        catch { }
    }

    [RelayCommand(CanExecute = nameof(CanOpenRdp))]
    private void OpenRdp()
    {
        if (SelectedResult is not { } r) return;
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start("mstsc", $"/v:{r.IP}");
            else if (OperatingSystem.IsLinux())
                Process.Start(new ProcessStartInfo("xfreerdp", $"/v:{r.IP}") { UseShellExecute = false });
            else if (OperatingSystem.IsMacOS())
                Process.Start(new ProcessStartInfo("open", $"-a \"Microsoft Remote Desktop\" --args {r.IP}") { UseShellExecute = false });
        }
        catch { }
    }

    [RelayCommand(CanExecute = nameof(CanCopyIp))]
    private async Task CopyIpAsync()
    {
        if (SelectedResult is not { } r) return;
        var window = (Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        var clipboard = (window as TopLevel)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(r.IP);
            StatusText = "Copied!";
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportHtmlAsync()
    {
        var window = (Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        var topLevel = TopLevel.GetTopLevel(window);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Gem scan som HTML",
            SuggestedFileName = $"netscan_{_lastScannedSubnet.Replace('.', '-')}_{_lastScannedAt:yyyyMMdd_HHmm}.html",
            DefaultExtension = "html",
            FileTypeChoices =
            [
                new Avalonia.Platform.Storage.FilePickerFileType("HTML-fil") { Patterns = ["*.html"] }
            ]
        });

        if (file == null) return;

        try
        {
            var html = NetScan.Core.HtmlExporter.Generate(Results, _lastScannedSubnet, _lastScannedAt);
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new System.IO.StreamWriter(stream, System.Text.Encoding.UTF8);
            await writer.WriteAsync(html);
            StatusText = "Exported!";
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed: {ex.Message}";
        }
    }

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    // Insert in ascending IP (numeric) order so the list stays sorted as results arrive
    private void InsertSorted(ScanResult result)
    {
        int i = 0;
        while (i < Results.Count && Results[i].IpSortKey < result.IpSortKey)
            i++;
        Results.Insert(i, result);
    }
}
