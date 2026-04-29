using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Diagnostics;

namespace NetScan.Core;

public partial class ScanEngine
{
    private static readonly Dictionary<int, string> PortNames = new()
    {
        { 80,   "HTTP"      },
        { 443,  "HTTPS"     },
        { 8080, "HTTP-Alt"  },
        { 22,   "SSH"       },
        { 3389, "RDP"       },
        { 445,  "SMB"       },
        { 139,  "NetBIOS"   },
        { 21,   "FTP"       },
        { 9100, "Print"     },
        { 515,  "LPD"       },
        { 5000, "UPnP"      },
        { 554,  "RTSP"      },
        { 161,  "SNMP"      },
        { 8443, "HTTPS-Alt" },
        { 3000, "Dev"       },
        { 5985, "WinRM"     }
    };

    private static readonly int[] WebPorts = [80, 443, 8080, 8443, 3000];

    private readonly OuiLookup _oui;

    public ScanEngine(OuiLookup oui)
    {
        _oui = oui;
    }

    public async IAsyncEnumerable<ScanResult> ScanAsync(
        string subnetBase,
        IProgress<int> progress,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<ScanResult>();
        var sem = new SemaphoreSlim(20, 20);

        // ARP table is read after a short delay so responding hosts have had
        // time to populate the kernel cache — responding LAN hosts answer in
        // <10 ms so 600 ms is more than enough, and we don't have to wait for
        // the full 1 s ping timeout of the 229 unreachable hosts.
        var arpTableTask = Task.Run(async () =>
        {
            await Task.Delay(600, ct);
            return await GetArpTableAsync();
        }, ct);

        _ = Task.Run(async () =>
        {
            int done = 0;
            var enrichTasks = new System.Collections.Concurrent.ConcurrentBag<Task>();

            // Ping all 254 hosts in parallel; as each one responds alive,
            // immediately start enrichment without waiting for the rest.
            var pingTasks = Enumerable.Range(1, 254).Select(async i =>
            {
                var ip = $"{subnetBase}.{i}";
                bool alive = await PingAsync(ip, ct);
                progress.Report(Interlocked.Increment(ref done));

                if (!alive) return;

                await sem.WaitAsync(ct);
                enrichTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        var arpTable = await arpTableTask;
                        var result = await EnrichAsync(ip, arpTable, ct);
                        await channel.Writer.WriteAsync(result, ct);
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                    finally { sem.Release(); }
                }, ct));
            });

            await Task.WhenAll(pingTasks);
            await Task.WhenAll(enrichTasks);
            channel.Writer.Complete();
        }, ct);

        await foreach (var result in channel.Reader.ReadAllAsync(ct))
            yield return result;
    }

    private async Task<ScanResult> EnrichAsync(string ip, Dictionary<string, string> arpTable, CancellationToken ct)
    {
        var result = new ScanResult { IP = ip };

        // MAC + vendor (instant, from ARP table already fetched)
        arpTable.TryGetValue(ip, out var mac);
        result.MAC = mac ?? string.Empty;
        if (!string.IsNullOrEmpty(result.MAC))
            result.Vendor = _oui.Lookup(result.MAC);

        // DNS and port scan in parallel — DNS gets a hard 1.5 s timeout so a
        // slow or missing reverse-DNS record doesn't stall the whole enrichment
        var dnsTask = Task.Run(async () =>
        {
            try
            {
                using var dnsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                dnsCts.CancelAfter(1500);
                var entry = await Dns.GetHostEntryAsync(ip, dnsCts.Token);
                result.Hostname = entry.HostName;
            }
            catch
            {
                result.Hostname = ip;
            }
        }, ct);

        var portTasks = PortNames.Keys.Select(async port =>
        {
            bool open = await IsPortOpenAsync(ip, port, ct);
            return (port, open);
        });

        var portResults = await Task.WhenAll(portTasks);
        await dnsTask;

        var openPorts = new List<(int port, string name)>();
        foreach (var (port, open) in portResults)
        {
            if (open && PortNames.TryGetValue(port, out var portName))
                openPorts.Add((port, portName));
        }

        openPorts.Sort((a, b) => a.port.CompareTo(b.port));
        result.Ports = string.Join(", ", openPorts.Select(p => $"{p.port}/{p.name}"));

        // HasRDP / HasSMB
        result.HasRDP = openPorts.Any(p => p.port == 3389);
        result.HasSMB = openPorts.Any(p => p.port == 445 || p.port == 139);

        // HasWeb and WebUrl
        var webPort = openPorts.FirstOrDefault(p => WebPorts.Contains(p.port));
        if (webPort != default)
        {
            result.HasWeb = true;
            result.WebUrl = BuildWebUrl(ip, webPort.port);
        }

        return result;
    }

    private static string BuildWebUrl(string ip, int port)
    {
        return port switch
        {
            80   => $"http://{ip}",
            443  => $"https://{ip}",
            8443 => $"https://{ip}:8443",
            _    => $"http://{ip}:{port}"
        };
    }

    private static async Task<bool> PingAsync(string ip, CancellationToken ct)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, 1000);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsPortOpenAsync(string ip, int port, CancellationToken ct)
    {
        try
        {
            using var tcp = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(300);
            await tcp.ConnectAsync(ip, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<Dictionary<string, string>> GetArpTableAsync()
    {
        var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var psi = new ProcessStartInfo("arp", "-a")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return table;

            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            foreach (Match m in ArpLineRegex().Matches(output))
            {
                var ip = m.Groups["ip"].Value;
                var mac = m.Groups["mac"].Value.Replace("-", ":").ToUpperInvariant();
                table.TryAdd(ip, mac);
            }
        }
        catch
        {
            // silently fail
        }
        return table;
    }

    [GeneratedRegex(@"(?<ip>\d{1,3}(?:\.\d{1,3}){3})\s+(?<mac>[0-9a-fA-F]{2}(?:[:-][0-9a-fA-F]{2}){5})", RegexOptions.Multiline)]
    private static partial Regex ArpLineRegex();

    public static string GetDefaultSubnet()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                        continue;

                    var ip = addr.Address.ToString();
                    // Skip APIPA (169.254.x.x)
                    if (ip.StartsWith("169.254"))
                        continue;

                    var parts = ip.Split('.');
                    if (parts.Length == 4)
                        return $"{parts[0]}.{parts[1]}.{parts[2]}";
                }
            }
        }
        catch
        {
            // fall through to default
        }
        return "192.168.1";
    }
}
