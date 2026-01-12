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

### 💾 Disk Information
For every mounted drive:
- Drive name  
- File system  
- Total space  
- Free space  

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

## 📄 Report Export

Every run generates two reports inside a `/reports` folder:

### 1️⃣ Text Report
Easy to read for humans:

reports/pc_diagnostic_YYYYMMDD_HHMMSS.txt

### 2️⃣ JSON Report
Structured machine-readable data:

reports/pc_diagnostic_YYYYMMDD_HHMMSS.json

The JSON includes:

{
  "GeneratedAt": "...",
  "MachineName": "...",
  "System": { ... },
  "Disks": [ ... ],
  "Network": { ... },
  "Battery": { ... }
}

This allows:
- GUI apps
- Remote monitoring
- Health scoring
- Cloud uploads
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

dotnet run

After it finishes:
- View the console output
- Open the `/reports` folder for your exported files

---

## 🧭 Roadmap

This tool is designed to grow into a full professional diagnostic suite.

Planned upgrades:
- Windows GUI (WPF)
- Wi-Fi SSID + signal strength
- Battery health scoring
- System health grading (green / yellow / red)
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
