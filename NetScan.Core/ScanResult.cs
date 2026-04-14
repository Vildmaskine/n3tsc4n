using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetScan.Core;

public class ScanResult : INotifyPropertyChanged
{
    private string _ip = string.Empty;
    private string _hostname = string.Empty;
    private string _mac = string.Empty;
    private string _vendor = string.Empty;
    private string _ports = string.Empty;
    private bool _hasWeb;
    private bool _hasRDP;
    private string _webUrl = string.Empty;

    public string IP
    {
        get => _ip;
        set { _ip = value; OnPropertyChanged(); }
    }

    public string Hostname
    {
        get => _hostname;
        set { _hostname = value; OnPropertyChanged(); }
    }

    public string MAC
    {
        get => _mac;
        set { _mac = value; OnPropertyChanged(); }
    }

    public string Vendor
    {
        get => _vendor;
        set { _vendor = value; OnPropertyChanged(); }
    }

    public string Ports
    {
        get => _ports;
        set { _ports = value; OnPropertyChanged(); }
    }

    public bool HasWeb
    {
        get => _hasWeb;
        set { _hasWeb = value; OnPropertyChanged(); }
    }

    public bool HasRDP
    {
        get => _hasRDP;
        set { _hasRDP = value; OnPropertyChanged(); }
    }

    public string WebUrl
    {
        get => _webUrl;
        set { _webUrl = value; OnPropertyChanged(); }
    }

    // Numeric sort key so 192.168.1.2 sorts before 192.168.1.10
    public uint IpSortKey
    {
        get
        {
            var parts = _ip.Split('.');
            if (parts.Length != 4) return 0;
            uint key = 0;
            foreach (var p in parts)
                key = key * 256 + (uint.TryParse(p, out var b) ? b : 0u);
            return key;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
