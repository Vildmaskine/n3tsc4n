using System.IO;
using System.Net.Http;

namespace NetScan.Core;

public class OuiLookup
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "N3tSc4n");
    private static readonly string DbPath = Path.Combine(DataDir, "oui.csv");
    private readonly Dictionary<string, string> _table = new(StringComparer.OrdinalIgnoreCase);

    public async Task InitializeAsync(IProgress<string>? progress = null)
    {
        bool needsDownload = !File.Exists(DbPath)
            || (DateTime.UtcNow - File.GetLastWriteTimeUtc(DbPath)).TotalDays > 30;

        if (needsDownload)
        {
            var reason = !File.Exists(DbPath) ? "not found" : "outdated (>30 days)";
            progress?.Report($"OUI database {reason} — downloading...");
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                var bytes = await client.GetByteArrayAsync("https://standards-oui.ieee.org/oui/oui.csv");
                Directory.CreateDirectory(DataDir);
                await File.WriteAllBytesAsync(DbPath, bytes);
                progress?.Report("OUI database downloaded — loading...");
            }
            catch
            {
                progress?.Report("Download failed — using existing database if available");
            }
        }
        else
        {
            progress?.Report("Loading OUI database...");
        }

        if (File.Exists(DbPath))
        {
            ParseOuiFile();
            progress?.Report($"Ready — {_table.Count:N0} vendors loaded");
        }
        else
        {
            progress?.Report("Ready (no OUI database)");
        }
    }

    private void ParseOuiFile()
    {
        _table.Clear();
        try
        {
            using var reader = new StreamReader(DbPath);
            bool firstLine = true;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (firstLine)
                {
                    firstLine = false;
                    continue; // skip header
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',', 4);
                if (parts.Length < 3)
                    continue;

                var oui = parts[1].Trim().Replace("-", "").Replace(":", "").ToUpperInvariant();
                var vendor = parts[2].Trim().Trim('"');

                if (oui.Length == 6 && !string.IsNullOrEmpty(vendor))
                {
                    _table.TryAdd(oui, vendor);
                }
            }
        }
        catch
        {
            // silently fail on parse errors
        }
    }

    public string Lookup(string mac)
    {
        if (string.IsNullOrEmpty(mac))
            return string.Empty;

        // Normalize: remove separators, take first 6 hex chars uppercase
        var normalized = mac.Replace(":", "").Replace("-", "").Replace(".", "").ToUpperInvariant();
        if (normalized.Length < 6)
            return string.Empty;

        var oui = normalized[..6];
        return _table.TryGetValue(oui, out var vendor) ? vendor : string.Empty;
    }
}
