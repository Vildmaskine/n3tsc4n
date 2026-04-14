# N3tSc4n

A fast network scanner with a dark UI. Discovers all live hosts on a `/24` subnet via ICMP ping, then enriches each host with hostname, MAC address, vendor (IEEE OUI), and open ports.

## Features

- **Ping sweep** — scans all 254 addresses in parallel
- **Port detection** — HTTP, HTTPS, SSH, RDP, SMB, FTP, and more
- **MAC & vendor lookup** — reads ARP table, looks up vendor via IEEE OUI database
- **Live results** — hosts populate the table as they are found
- **One-click actions** — open web interface, launch RDP, or copy IP to clipboard
- **Cross-platform** — native WPF app on Windows, Avalonia UI on Linux and macOS

## Download

Grab the latest release for your platform from [Releases](../../releases/latest):

| Platform | File |
|---|---|
| Windows | `NetScan.exe` |
| Linux | `NetScan-x86_64.AppImage` |
| macOS (Intel) | `NetScan` (x64) |
| macOS (Apple Silicon) | `NetScan` (arm64) |

All builds are self-contained — no .NET installation required.

### Linux

```bash
chmod +x NetScan-x86_64.AppImage
./NetScan-x86_64.AppImage
```

> The scanner uses `arp -a` to read the ARP table and `xfreerdp` for RDP. Install them if needed:
> ```bash
> sudo apt install net-tools xfreerdp2-x11   # Debian/Ubuntu
> sudo dnf install net-tools freerdp          # Fedora
> ```

### macOS

```bash
chmod +x NetScan
./NetScan
```

> RDP requires **Microsoft Remote Desktop** from the App Store.

## Usage

1. The detected subnet is pre-filled — change it if needed
2. Click **▶ Scan** to start
3. Click a row to enable the action buttons:
   - **🌐 Web** — opens the host's web interface in your browser
   - **🖥 RDP** — launches a Remote Desktop session
   - **📋 IP** — copies the IP address to clipboard
4. Double-click a row to open its web interface directly
5. Click **■ Stop** to cancel a running scan

> **⚠ Subnet warning** — if you scan a different subnet than your own, MAC addresses and vendor info will not be available (ARP only works within the same network segment). Ping, ports and hostname still work.

## Building from source

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/vildmaskine/n3tsc4n
cd n3tsc4n

# Windows
dotnet publish NetScan.Windows/NetScan.Windows.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Linux
dotnet publish NetScan.CrossPlatform/NetScan.CrossPlatform.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true

# macOS
dotnet publish NetScan.CrossPlatform/NetScan.CrossPlatform.csproj -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true
```

## Solution structure

```
NetScan.sln
├── NetScan.Core/           — scanning engine, OUI lookup, data model (net8.0)
├── NetScan.Windows/        — WPF frontend for Windows (net9.0-windows)
└── NetScan.CrossPlatform/  — Avalonia frontend for Linux & macOS (net9.0)
```

## License

MIT
