# 🖥️ PC Diagnostic Tool

A local-first, cross-platform **C# / .NET 10** diagnostic application for **macOS and Windows**. Run a command-line scan to export system reports, or use the built-in **web dashboard** to inspect hardware, performance, connectivity, battery information, and running processes.

> **Status:** Actively developed. The dashboard and process monitoring have been tested on macOS; Windows-specific behavior still needs broader testing. Some readings depend on operating-system permissions and available hardware.

## ✨ Features

### Local web dashboard
- Dark-themed dashboard at **http://127.0.0.1:5050/**
- System overview with health scores and component summaries
- Dedicated **CPU, Memory, Storage, Network, Battery, and Processes** sections
- Detailed system information and report views
- **Run new scan** and **Reload report** controls
- Automatic full scans approximately every **5 minutes** while the browser tab is visible
- Live metrics and a read-only process list that refreshes approximately every **10 seconds** while the tab is visible

### 🧠 System and CPU
- OS version, CPU architecture and model, logical processor count, RAM, uptime, last boot time, and .NET runtime information
- CPU utilization and detailed CPU diagnostics, when supported

### 🧮 Memory
- Total and used memory, utilization, and additional memory/pressure details where the operating system provides them

### 💾 Storage
- Mounted drives, filesystem, total/free capacity
- Physical disk model, type (SSD/HDD), interface, TRIM, and SMART/health information where available
- Platform-specific collection using macOS utilities and Windows management interfaces

### 🌐 Network and Wi-Fi
- Hostname, adapters, IP addresses, gateway, DNS, link details, and connectivity checks
- Gateway and public-IP ping tests
- Wi-Fi details such as SSID, signal, channel, security, and link characteristics **when accessible**

**macOS Wi-Fi privacy note:** Modern macOS versions can redact the SSID/BSSID or restrict other Wi-Fi details because of Location Services and privacy protections. Running the app with `sudo` does **not** reliably bypass those protections. The dashboard should indicate when data is unavailable or restricted; it must not invent a network name. Windows uses a separate collection path and should not display macOS-specific restriction messaging.

### 🔋 Battery
- Presence, current charge, charging state, condition, and cycle count
- Design capacity, full-charge capacity, and estimated battery health when reported by the device
- Dashboard charge and estimated-health progress bars
- macOS data sources include `pmset`, `system_profiler`, and `ioreg`; Windows battery information uses Windows APIs/WMI where available
- Desktops or devices that do not expose battery measurements may show **N/A**

### ▦ Running processes
- Read-only table of the **top 15 accessible processes**
- Process name, process ID (PID), CPU utilization, and working-set memory
- Sort by highest CPU or highest memory usage
- CPU percentage is normalized to **total logical CPU capacity** over a short sample interval
- Protected or inaccessible processes may be omitted; there are **no process termination controls**

### ❤️ Health scoring
Component-level status, score, and explanations for supported CPU, memory, disk, network, Wi-Fi, and battery checks, plus an overall health summary. Missing or unsupported measurements should be treated as unknown rather than assumed healthy.

## 🧰 Requirements

- **.NET 10 SDK**
- **macOS or Windows**
- Terminal access
- A modern web browser for the dashboard

Dependencies are declared in the project files and restored automatically by `dotnet`; you do not need to manually add `System.Management`.

## ▶️ Getting started

From the repository root:

```bash
git clone https://github.com/Ethan-t-n/PC_Diagnostic_Tool.git
cd PC_Diagnostic_Tool
dotnet restore
```

### Option A — Open the dashboard (recommended)

```bash
dotnet run --project Dashboard/Dashboard.csproj
```

Open **http://127.0.0.1:5050/** in your browser.

Use **Run new scan** to generate a fresh report, or **Reload report** to display the most recent saved report. Stop the server with **Ctrl+C** in the terminal.

The dashboard binds to **127.0.0.1** (your own computer), not a public network interface.

### Option B — Run a command-line scan

```bash
dotnet run --project PC_Diagnostic_Tool.csproj
```

The scan prints diagnostic information and writes reports to the `reports/` directory.

### Build checks

```bash
dotnet build PC_Diagnostic_Tool.csproj
dotnet build Dashboard/Dashboard.csproj
```

## 📄 Reports

Each command-line scan generates timestamped reports:

```text
reports/
  pc_diagnostic_YYYYMMDD_HHMMSS.txt
  pc_diagnostic_YYYYMMDD_HHMMSS.json
```

- **TXT:** human-readable diagnostic summary
- **JSON:** structured results used by the dashboard and suitable for further analysis

The dashboard reads the latest saved JSON report. Live process samples are collected through a separate local endpoint and are not necessarily included in the exported report.

## 🔒 Privacy and limitations

- The dashboard is served **locally** at `127.0.0.1:5050`.
- Diagnostic reports may contain sensitive system or network information; review them before sharing.
- Generated `reports/` files are excluded from Git by the project's ignore rules.
- Hardware details, SMART data, temperatures, Wi-Fi fields, and battery capacities vary by platform and permissions.
- This is a diagnostic and informational tool, not a substitute for professional hardware testing.

## 🧭 Roadmap

Potential future improvements (not yet implemented):

- Historical scan browsing, comparisons, and performance trend graphs
- More comprehensive hardware temperature monitoring where supported
- Expanded Windows compatibility and automated cross-platform tests
- CSV/PDF export
- Additional diagnostic explanations and troubleshooting guidance

## 🤝 Development

Development is organized into small feature branches and pull requests. The application currently includes a .NET command-line collector and a separate ASP.NET Core dashboard.

Contributions, bug reports, and platform-specific testing feedback are welcome.
