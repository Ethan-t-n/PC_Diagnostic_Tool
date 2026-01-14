# 🖥️ PC Diagnostic Tool

A cross-platform (Windows + macOS) command-line diagnostic tool written in **C# (.NET)** that scans your system, network, storage, performance, and battery health — then exports everything into **human-readable** and **machine-readable** reports.

This project is designed to grow into a full desktop diagnostic application (GUI, health scoring, etc.), but already provides powerful real-world diagnostics.

---

## 🚀 What This Tool Does

When you run the app, it automatically collects:

### 🧠 System Information
- OS name & version  
- CPU architecture & core count  
- CPU model (Intel / AMD / Apple Silicon)  
- Total RAM  
- System uptime  
- Last boot time  
- .NET runtime version  

### 📊 Live Performance
- CPU usage %
- RAM usage %

(Works on macOS and Windows using native OS tools)

---

## 💾 Disk Information

### Logical drives
For every mounted drive:
- Drive name  
- File system  
- Total space  
- Free space

### Physical storage (SSDs / HDDs)
- Disk name & model  
- Capacity  
- SSD vs HDD detection  
- TRIM support  
- SMART status (when available)  
- Interface type  
- Health status  

Supports:
- macOS via `diskutil` + `system_profiler`
- Windows via WMI

---

### 🌐 Network Diagnostics
- Hostname  
- Primary network adapter  
- IPv4 & IPv6 addresses  
- Default gateway  
- DNS servers  

Connectivity tests:
- Ping your gateway  
- Ping `1.1.1.1` (Cloudflare)  

Active adapters:
- Adapter name & type  
- MAC address  
- Link speed  
- IP addresses  
- Gateways  
- DNS servers

---

## 📡 Wi-Fi (macOS + Windows)
- SSID  
- Signal strength (%)  
- RSSI (dBm)  
- Noise  
- Channel  
- PHY mode  
- Security type  
- TX rate  

On macOS, Wi-Fi data requires admin privileges.

Run with:

`sudo dotnet run`

---

### 🔋 Battery Health (Laptops)
On supported systems:
- Battery present or not  
- Charge percentage  
- Charging / discharging  
- Cycle count  
- Battery condition  
- Full charge capacity  
- Design capacity  

Supports:
- **macOS** (via `pmset` + `system_profiler`)
- **Windows** (via WMI `Win32_Battery`)

---

## ❤️ Health Score System

Each scan produces a full health report:

| Component | What is evaluated |
|--------|------------------|
| CPU | Current usage |
| RAM | Memory pressure |
| Disk | Free space on system drive |
| Network | Internet connectivity |
| Wi-Fi | Signal strength |
| Battery | Condition & presence |

Each category gets:
- Score (0–100)
- Color status (Green / Yellow / Red / Unknown)
- Human-readable explanation  

An overall system health score is calculated.

Example output:

| Component | Status | Score | Details |
|---------|--------|-------|---------|
| CPU | Green | 95/100 | 35.8% usage (healthy) |
| RAM | Red | 25/100 | 91% used (very tight) |
| Disk | Yellow | 60/100 | 15% free (low) |
| Network | Green | 95/100 | Internet OK |
| Wi-Fi | Green | 95/100 | Strong signal |
| Battery | Green | 95/100 | Condition: Normal |

Overall Health: **74/100 (Yellow)**


## 📄 Report Export

Every run generates two reports inside a `/reports` folder:

### 1️⃣ Text Report
Easy to read for humans:

reports/pc_diagnostic_YYYYMMDD_HHMMSS.txt

### 2️⃣ JSON Report
Structured machine-readable data:

reports/pc_diagnostic_YYYYMMDD_HHMMSS.json

The JSON includes:

```json
{
  "GeneratedAt": "...",
  "MachineName": "...",
  "System": { ... },
  "Disks": [ ... ],
  "Network": { ... },
  "Battery": { ... },
  "Health": { ... }
}
```

This allows:
- GUI apps
- Remote monitoring
- Health scoring
- Cloud uploads
- Trend tracking
- Future dashboards

---

## 🛡️ Privacy & Git Safety

The `/reports` folder is excluded from Git using `.gitignore`, so your personal machine data is **never** pushed to GitHub.

---

## 🧰 Requirements

- .NET 8+ (or newer)
- macOS or Windows  
- Terminal access  

For Windows battery & performance:
dotnet add package System.Management

---

## ▶️ How to Run

`dotnet run`

For full Wi-Fi access on macOS:

`sudo dotnet run`

After it finishes:
- View the console output
- Open the `/reports` folder for your exported files

---

## 🧭 Roadmap

This tool is designed to grow into a full professional diagnostic suite.

Planned upgrades:
- Windows GUI (WPF)
- Historical health tracking
- CPU thermals
- Memory pressure metrics
- Battery wear scoring
- Wi-Fi quality graphs
- Performance graphs
- Save history
- Export to CSV / PDF

---

## ✨ Why This Exists

Most PC diagnostic tools are:
- Bloated
- Closed-source
- Full of ads
- Hard to automate
