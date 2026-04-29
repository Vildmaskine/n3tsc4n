using System.Text;

namespace NetScan.Core;

public static class HtmlExporter
{
    public static string Generate(IEnumerable<ScanResult> results, string subnet, DateTime scannedAt)
    {
        var rows = results.ToList();
        int webCount  = rows.Count(r => r.HasWeb);
        int rdpCount  = rows.Count(r => r.HasRDP);
        int sshCount  = rows.Count(r => ContainsPort(r.Ports, "SSH"));

        var sb = new StringBuilder();
        sb.Append($"""
            <!DOCTYPE html>
            <html lang="da">
            <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>N3tSc4n — {subnet}.0/24</title>
            <style>
            :root {{
                --base:     #1e1e2e;
                --mantle:   #181825;
                --surface0: #313244;
                --surface1: #45475a;
                --overlay0: #6c7086;
                --text:     #cdd6f4;
                --subtext0: #a6adc8;
                --blue:     #89b4fa;
                --green:    #a6e3a1;
                --yellow:   #f9e2af;
                --mauve:    #cba6f7;
                --red:      #f38ba8;
            }}
            *{{ box-sizing:border-box; margin:0; padding:0; }}
            body{{ background:var(--base); color:var(--text); font-family:'Segoe UI',system-ui,sans-serif; font-size:14px; }}
            header{{ background:var(--mantle); border-bottom:1px solid var(--surface0); padding:20px 32px; display:flex; justify-content:space-between; align-items:center; }}
            .logo{{ display:flex; align-items:center; gap:12px; }}
            .logo-text{{ font-family:'Consolas','Courier New',monospace; font-size:22px; font-weight:600; }}
            .meta{{ text-align:right; color:var(--subtext0); font-size:13px; line-height:1.8; }}
            .meta strong{{ color:var(--blue); }}
            main{{ padding:24px 32px; }}
            .stats{{ display:flex; gap:12px; margin-bottom:24px; flex-wrap:wrap; }}
            .stat{{ background:var(--surface0); border:1px solid var(--surface1); border-radius:8px; padding:12px 20px; min-width:110px; }}
            .stat .lbl{{ color:var(--subtext0); font-size:11px; text-transform:uppercase; letter-spacing:.5px; margin-bottom:4px; }}
            .stat .val{{ color:var(--blue); font-size:24px; font-weight:700; font-family:'Consolas',monospace; }}
            table{{ width:100%; border-collapse:collapse; border:1px solid var(--surface0); border-radius:8px; overflow:hidden; }}
            thead th{{ background:var(--mantle); color:var(--blue); font-weight:600; font-size:12px; padding:10px 14px; text-align:left; border-bottom:1px solid var(--surface0); white-space:nowrap; }}
            tbody tr{{ background:var(--base); }}
            tbody tr:nth-child(even){{ background:var(--mantle); }}
            tbody tr:hover{{ background:var(--surface0); transition:background .1s; }}
            td{{ padding:9px 14px; border-bottom:1px solid var(--surface0); vertical-align:middle; }}
            .ip{{ font-family:'Consolas',monospace; }}
            .mac{{ font-family:'Consolas',monospace; color:var(--subtext0); font-size:12px; }}
            .vendor{{ color:var(--subtext0); }}
            .dim{{ color:var(--overlay0); }}
            a{{ color:var(--blue); text-decoration:none; }}
            a:hover{{ text-decoration:underline; }}
            .badges{{ display:flex; gap:3px; flex-wrap:wrap; }}
            .badge{{ display:inline-block; border-radius:4px; padding:1px 7px; font-family:'Consolas',monospace; font-size:11px; white-space:nowrap; border:1px solid; }}
            .b-http  {{ background:#1a3a2a; border-color:var(--green);  color:var(--green);  }}
            .b-https {{ background:#1a2a40; border-color:var(--blue);   color:var(--blue);   }}
            .b-rdp   {{ background:#2a1a3a; border-color:var(--mauve);  color:var(--mauve);  }}
            .b-ssh   {{ background:#2a2a1a; border-color:var(--yellow); color:var(--yellow); }}
            .b-smb   {{ background:#2a1a1a; border-color:var(--red);    color:var(--red);    }}
            .b-other {{ background:var(--surface0); border-color:var(--surface1); color:var(--subtext0); }}
            footer{{ margin-top:32px; padding:16px 32px; border-top:1px solid var(--surface0); color:var(--overlay0); font-size:12px; text-align:center; }}
            @media print{{
                body,header,main,footer{{ background:white!important; color:#111!important; }}
                .stat,.badge{{ border-color:#ccc!important; background:#f5f5f5!important; color:#111!important; }}
                a{{ color:#1a5fb4!important; }}
                thead th{{ background:#f0f0f0!important; color:#1a5fb4!important; }}
                tbody tr:nth-child(even){{ background:#fafafa!important; }}
                tbody tr:hover{{ background:#f0f0f0!important; }}
                .val,.logo-text{{ color:#1a5fb4!important; }}
                .mac,.vendor,.dim{{ color:#555!important; }}
                .meta{{ color:#555!important; }}
                .meta strong{{ color:#1a5fb4!important; }}
                td,th{{ border-color:#ddd!important; }}
                table{{ border-color:#ddd!important; }}
            }}
            </style>
            </head>
            <body>
            <header>
              <div class="logo">
                <svg width="26" height="26" viewBox="0 0 22 22" xmlns="http://www.w3.org/2000/svg">
                  <ellipse cx="5" cy="11" rx="2" ry="2" fill="#89b4fa"/>
                  <path d="M 5,6 A 5,5 0 0 1 5,16" stroke="#89b4fa" stroke-width="1.8" fill="none"/>
                  <path d="M 5,3 A 8,8 0 0 1 5,19" stroke="#89b4fa" stroke-width="1.8" fill="none" opacity="0.6"/>
                  <path d="M 5,0 A 11,11 0 0 1 5,22" stroke="#89b4fa" stroke-width="1.8" fill="none" opacity="0.3"/>
                </svg>
                <span class="logo-text">N3tSc4n</span>
              </div>
              <div class="meta">
                <div>Subnet: <strong>{subnet}.0/24</strong></div>
                <div>Skannet: <strong>{scannedAt:dd. MMMM yyyy, HH:mm}</strong></div>
              </div>
            </header>
            <main>
              <div class="stats">
                <div class="stat"><div class="lbl">Enheder fundet</div><div class="val">{rows.Count}</div></div>
                <div class="stat"><div class="lbl">Web-interface</div><div class="val">{webCount}</div></div>
                <div class="stat"><div class="lbl">RDP</div><div class="val">{rdpCount}</div></div>
                <div class="stat"><div class="lbl">SSH</div><div class="val">{sshCount}</div></div>
              </div>
              <table>
                <thead>
                  <tr>
                    <th>IP</th>
                    <th>Hostname</th>
                    <th>MAC</th>
                    <th>Producent</th>
                    <th>Porte</th>
                  </tr>
                </thead>
                <tbody>
            """);

        foreach (var r in rows)
        {
            var ipCell = r.HasWeb
                ? $"""<a href="{r.WebUrl}">{r.IP}</a>"""
                : r.IP;

            var hostnameCell = string.IsNullOrEmpty(r.Hostname) || r.Hostname == r.IP
                ? $"""<span class="dim">—</span>"""
                : r.Hostname;

            var macCell  = string.IsNullOrEmpty(r.MAC)    ? """<span class="dim">—</span>""" : r.MAC;
            var vendorCell = string.IsNullOrEmpty(r.Vendor) ? """<span class="dim">—</span>""" : r.Vendor;

            sb.AppendLine($"""
                    <tr>
                      <td class="ip">{ipCell}</td>
                      <td class="hostname">{hostnameCell}</td>
                      <td class="mac">{macCell}</td>
                      <td class="vendor">{vendorCell}</td>
                      <td>{BuildPortBadges(r.Ports)}</td>
                    </tr>
                """);
        }

        sb.Append($"""
                </tbody>
              </table>
            </main>
            <footer>Genereret af N3tSc4n · {scannedAt:yyyy-MM-dd HH:mm}</footer>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    private static string BuildPortBadges(string ports)
    {
        if (string.IsNullOrWhiteSpace(ports))
            return """<span class="dim">—</span>""";

        var badges = new StringBuilder("""<div class="badges">""");
        foreach (var entry in ports.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var cls = BadgeClass(entry);
            badges.Append($"""<span class="badge {cls}">{entry}</span>""");
        }
        badges.Append("</div>");
        return badges.ToString();
    }

    private static string BadgeClass(string entry)
    {
        var u = entry.ToUpperInvariant();
        if (u.Contains("HTTPS")) return "b-https";
        if (u.Contains("HTTP"))  return "b-http";
        if (u.Contains("RDP"))   return "b-rdp";
        if (u.Contains("SSH"))   return "b-ssh";
        if (u.Contains("SMB") || u.Contains("NETBIOS")) return "b-smb";
        return "b-other";
    }

    private static bool ContainsPort(string ports, string service) =>
        !string.IsNullOrEmpty(ports) &&
        ports.Contains(service, StringComparison.OrdinalIgnoreCase);
}
