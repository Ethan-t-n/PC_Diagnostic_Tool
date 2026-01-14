using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;




public class Program
{

    static double? _lastCpuPct;
    static double? _lastRamPct;

    static void Main()
    {
        var report = new StringBuilder();

        void Log(string line = "")
        {
            Console.WriteLine(line);
            report.AppendLine(line);
        }

        Log("PC Diagnostic Tool v0.3");
        Log("------------------------");

        PrintBasicSystemInfo(Log);
        PrintDiskInfo(Log);
        PrintStorageInfo(Log);
        PrintNetworkInfo(Log);
        PrintBatteryInfo(Log);
        PrintHealthScore(Log);



        Log();
        Log("Scan complete.");

        ExportReports(report.ToString());
    }

    class DiagnosticReport
    {
        public string GeneratedAt { get; set; } = "";
        public string MachineName { get; set; } = "";
        public SystemInfo System { get; set; } = new();
        public List<DiskInfo> Disks { get; set; } = new();
        public NetworkInfo Network { get; set; } = new();
        public BatteryInfo Battery { get; set; } = new();
        public StorageInfo Storage { get; set; } = new();
        public HealthSummary Health { get; set; } = new();



    }

    class SystemInfo
    {
        public string OsDescription { get; set; } = "";
        public string Architecture { get; set; } = "";
        public string UserName { get; set; } = "";
        public int CpuCores { get; set; }
        public string CpuModel { get; set; } = "";
        public long? RamBytes { get; set; }
        public long UptimeSeconds { get; set; }
        public string LastBootLocal { get; set; } = "";
        public string DotNetVersion { get; set; } = "";
    }

    class DiskInfo
    {
        public string Name { get; set; } = "";
        public string Format { get; set; } = "";
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
    }

    class NetworkInfo
    {
        public string Host { get; set; } = "";
        public WifiInfo Wifi { get; set; } = new();

        public string PrimaryAdapterName { get; set; } = "";
        public string PrimaryAdapterType { get; set; } = "";
        public string? IPv4 { get; set; }
        public string? IPv6 { get; set; }
        public string? Gateway { get; set; }
        public List<string> DnsServers { get; set; } = new();
        public PingResults Pings { get; set; } = new();
        public List<AdapterInfo> ActiveAdapters { get; set; } = new();
    }

    class AdapterInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "";
        public string Status { get; set; } = "";
        public string Mac { get; set; } = "";
        public long? LinkSpeedMbps { get; set; }
        public List<string> IpAddresses { get; set; } = new();
        public List<string> Gateways { get; set; } = new();
        public List<string> DnsServers { get; set; } = new();
    }

    public class BatteryInfo
    {
        public bool IsPresent { get; set; }
        public int? Percentage { get; set; }
        public bool? IsCharging { get; set; }
        public string? Condition { get; set; }         
        public int? CycleCount { get; set; }            
        public int? DesignCapacitymAh { get; set; }     
        public int? FullChargeCapacitymAh { get; set; } 
        public string? Source { get; set; }             
        public string? Notes { get; set; }
    }

    public class WifiInfo
    {
        public bool IsAvailable { get; set; }
        public string? InterfaceName { get; set; }
        public string? Ssid { get; set; }
        public int? SignalPercent { get; set; }
        public int? RssiDbm { get; set; }
        public int? NoiseDbm { get; set; }
        public double? TxRateMbps { get; set; }
        public string? Channel { get; set; }
        public string? PhyMode { get; set; }
        public string? Security { get; set; }

        public string? Source { get; set; }
        public string? Notes { get; set; }
    }

    public class StorageInfo
    {
        public bool IsAvailable { get; set; }
        public List<PhysicalDiskInfo> Disks { get; set; } = new();
        public string? Source { get; set; }
        public string? Notes { get; set; }
    }

    public class PhysicalDiskInfo
    {
        public string? Name { get; set; }
        public string? Model { get; set; }
        public string? Serial { get; set; }

        public bool? IsSsd { get; set; }

        public string? CapacityText { get; set; }
        public string? SmartStatus { get; set; }
        public string? TrimSupport { get; set; }

        public long? CapacityBytes { get; set; }
        public string? MediumType { get; set; }
        public string? InterfaceType { get; set; }
        public string? HealthStatus { get; set; }
    }

    public enum HealthLevel
    {
        Green,
        Yellow,
        Red,
        Unknown
    }

    public class HealthItem
    {
        public string Name { get; set; } = "";
        public HealthLevel Level { get; set; } = HealthLevel.Unknown;
        public int Score { get; set; } // 0-100
        public string Message { get; set; } = "";
    }

    public class HealthSummary
    {
        public int OverallScore { get; set; }
        public HealthLevel OverallLevel { get; set; } = HealthLevel.Unknown;
        public List<HealthItem> Items { get; set; } = new();
    }



    class PingResults
    {
        public string? GatewayPing { get; set; }
        public string InternetPing { get; set; } = "";
    }

    static DiagnosticReport BuildStructuredReport()
    {
        var uptime = GetSystemUptime();
        var battery = BatteryCollector.GetBatteryInfo();
        var storage = SsdCollector.GetStorageInfo();
        var network = BuildNetworkInfo();
        var health = BuildHealthSummary();




        var system = new SystemInfo
        {
            OsDescription = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            UserName = Environment.UserName,
            CpuCores = Environment.ProcessorCount,
            CpuModel = GetCpuModel(),
            RamBytes = GetTotalRamBytes(),
            UptimeSeconds = (long)uptime.TotalSeconds,
            LastBootLocal = GetLastBootTime(uptime),
            DotNetVersion = Environment.Version.ToString()
        };

        var disks = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new DiskInfo
            {
                Name = d.Name,
                Format = d.DriveFormat,
                TotalBytes = d.TotalSize,
                FreeBytes = d.AvailableFreeSpace
            })
            .ToList();


        return new DiagnosticReport
        {
            GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            MachineName = Environment.MachineName,
            System = system,
            Disks = disks,
            Network = network,
            Battery = battery,
            Storage = storage,
            Health = health
        };
    }

    static NetworkInfo BuildNetworkInfo()
    {
        var result = new NetworkInfo
        {
            Host = Dns.GetHostName()
        };

        result.Wifi = WifiCollector.GetWifiInfo();


        var activeAdapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .Select(nic => new
            {
                Nic = nic,
                Props = nic.GetIPProperties(),
                Unicast = nic.GetIPProperties().UnicastAddresses
                    .Select(u => u.Address)
                    .Where(a => !IPAddress.IsLoopback(a) && !IsLinkLocal(a))
                    .ToList()
            })
            .Where(x => x.Unicast.Count > 0)
            .ToList();

        if (activeAdapters.Count == 0)
        {
            result.Pings.InternetPing = PingHost("1.1.1.1");
            return result;
        }

        var primary = activeAdapters
            .OrderByDescending(x => x.Props.GatewayAddresses.Any(g => g.Address != null && !IPAddress.IsLoopback(g.Address)))
            .First();

        result.PrimaryAdapterName = primary.Nic.Name;
        result.PrimaryAdapterType = primary.Nic.NetworkInterfaceType.ToString();

        var primaryGateway = primary.Props.GatewayAddresses
            .Select(g => g.Address)
            .FirstOrDefault(a => a != null && !IPAddress.IsLoopback(a));

        result.Gateway = primaryGateway?.ToString();

        result.DnsServers = primary.Props.DnsAddresses
            .Where(a => a != null && !IPAddress.IsLoopback(a))
            .Select(a => a.ToString())
            .ToList();

        result.IPv4 = primary.Unicast
            .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            ?.ToString();

        result.IPv6 = primary.Unicast
            .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            ?.ToString();

        result.Pings.GatewayPing = primaryGateway != null ? PingHost(primaryGateway.ToString()) : null;
        result.Pings.InternetPing = PingHost("1.1.1.1");

        foreach (var x in activeAdapters)
        {
            var nic = x.Nic;
            var props = x.Props;

            var adapter = new AdapterInfo
            {
                Name = nic.Name,
                Description = nic.Description,
                Type = nic.NetworkInterfaceType.ToString(),
                Status = nic.OperationalStatus.ToString(),
                Mac = FormatMac(nic.GetPhysicalAddress()),
                LinkSpeedMbps = nic.Speed > 0 ? nic.Speed / 1_000_000 : null,
                IpAddresses = x.Unicast.Select(ip => ip.ToString()).ToList(),
                Gateways = props.GatewayAddresses
                    .Select(g => g.Address)
                    .Where(a => a != null && !IPAddress.IsLoopback(a))
                    .Select(a => a!.ToString())
                    .ToList(),
                DnsServers = props.DnsAddresses
                    .Where(a => a != null && !IPAddress.IsLoopback(a))
                    .Select(a => a.ToString())
                    .ToList()
            };

            result.ActiveAdapters.Add(adapter);
        }

        return result;
    }

    static HealthSummary BuildHealthSummary()
    {
        var items = new List<HealthItem>();

        var cpuPct = _lastCpuPct;
        var ramPct = _lastRamPct;


        items.Add(ScoreCpu(cpuPct));
        items.Add(ScoreRam(ramPct));

        items.Add(ScoreDiskFree());

        items.Add(ScoreNetwork());

        var wifi = WifiCollector.GetWifiInfo();
        items.Add(ScoreWifi(wifi));

        var batt = BatteryCollector.GetBatteryInfo();
        items.Add(ScoreBattery(batt));

        var scored = items.Where(i => i.Level != HealthLevel.Unknown).ToList();
        var overallScore = scored.Count > 0 ? (int)Math.Round(scored.Average(i => i.Score)) : 0;

        var overallLevel = overallScore switch
        {
            >= 80 => HealthLevel.Green,
            >= 55 => HealthLevel.Yellow,
            _ => HealthLevel.Red
        };

        return new HealthSummary
        {
            Items = items,
            OverallScore = overallScore,
            OverallLevel = scored.Count == 0 ? HealthLevel.Unknown : overallLevel
        };
    }






    static void ExportReports(string textReport)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var folder = Path.Combine(Environment.CurrentDirectory, "reports");
            Directory.CreateDirectory(folder);

            var txtPath = Path.Combine(folder, $"pc_diagnostic_{timestamp}.txt");
            File.WriteAllText(txtPath, textReport);

            var jsonPath = Path.Combine(folder, $"pc_diagnostic_{timestamp}.json");
            var reportObj = BuildStructuredReport();
            var json = JsonSerializer.Serialize(reportObj, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(jsonPath, json);

            Console.WriteLine($"\nReports saved:");
            Console.WriteLine($"- {txtPath}");
            Console.WriteLine($"- {jsonPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nFailed to export reports: {ex.Message}");
        }
    }


    static void PrintBasicSystemInfo(Action<string> Log)
    {
        Log("\n== System Info ==");

        Log($"OS: {RuntimeInformation.OSDescription}");
        Log($"Architecture: {RuntimeInformation.OSArchitecture}");
        Log($"Machine Name: {Environment.MachineName}");
        Log($"User: {GetDisplayUserName()}");
        Log($"CPU Cores: {Environment.ProcessorCount}");

        var cpuModel = GetCpuModel();
        if (!string.IsNullOrWhiteSpace(cpuModel))
            Log($"CPU Model: {cpuModel}");

        var ramBytes = GetTotalRamBytes();
        if (ramBytes.HasValue)
            Log($"RAM: {ramBytes.Value / 1024 / 1024 / 1024} GB");

        var uptime = GetSystemUptime();
        Log($"Uptime: {FormatUptime(uptime)}");
        Log($"Last Boot: {GetLastBootTime(uptime)}");

        Log($".NET Version: {Environment.Version}");

        var (cpuPct, ramPct) = PerformanceCollector.GetCpuAndRamUsage();
        _lastCpuPct = cpuPct;
        _lastRamPct = ramPct;

        if (cpuPct.HasValue) Log($"CPU Usage: {cpuPct.Value:F1}%");
        if (ramPct.HasValue) Log($"RAM Usage: {ramPct.Value:F1}%");



    }

    static void PrintDiskInfo(Action<string> Log)
    {
        Log("\n== Disk Info ==");

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;

            Log($"Drive: {drive.Name}");
            Log($"  Format: {drive.DriveFormat}");
            Log($"  Total: {ToGb(drive.TotalSize)} GB");
            Log($"  Free:  {ToGb(drive.AvailableFreeSpace)} GB");
        }
    }

    static void PrintNetworkInfo(Action<string> Log)
    {
        Log("\n== Network Info ==");

        Log($"Host: {Dns.GetHostName()}");

        var wifi = BuildNetworkInfo().Wifi;
        if (wifi.IsAvailable)
        {
            Log($"Wi-Fi SSID: {wifi.Ssid ?? "(unknown)"}");

            if (wifi.SignalPercent.HasValue)
                Log($"Wi-Fi Signal: {wifi.SignalPercent.Value}%");

            if (wifi.RssiDbm.HasValue)
                Log($"Wi-Fi RSSI: {wifi.RssiDbm.Value} dBm");
        }
        else
        {
            Log("Wi-Fi: (not connected or not available)");
            if (!string.IsNullOrWhiteSpace(wifi.Notes))
                Log($"Wi-Fi Notes: {wifi.Notes}");
        }


        var activeAdapters = NetworkInterface.GetAllNetworkInterfaces()
        .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
        .Select(nic => new
        {
            Nic = nic,
            Props = nic.GetIPProperties(),
            Unicast = nic.GetIPProperties().UnicastAddresses
                .Select(u => u.Address)
                .Where(a => !IPAddress.IsLoopback(a) && !IsLinkLocal(a))
                .ToList()
        })
        .Where(x => x.Unicast.Count > 0)
        .ToList();

        if (activeAdapters.Count == 0)

        {
            Log("No active network adapters found.");
            return;
        }

        var primary = activeAdapters
        .OrderByDescending(x => x.Props.GatewayAddresses.Any(g => g.Address != null && !IPAddress.IsLoopback(g.Address)))
        .First();

        var primaryGateway = primary.Props.GatewayAddresses
            .Select(g => g.Address)
            .FirstOrDefault(a => a != null && !IPAddress.IsLoopback(a));

        var primaryDns = primary.Props.DnsAddresses
            .Where(a => a != null && !IPAddress.IsLoopback(a))
            .Select(a => a.ToString())
            .ToList();

        var bestIPv4 = primary.Unicast.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        var bestIPv6 = primary.Unicast.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);

        Log("\n-- Network Summary --");
        Log($"Primary Adapter: {primary.Nic.Name} ({primary.Nic.NetworkInterfaceType})");
        Log($"IPv4: {(bestIPv4?.ToString() ?? "(none)")}");
        Log($"IPv6: {(bestIPv6?.ToString() ?? "(none)")}");
        Log($"Gateway: {(primaryGateway?.ToString() ?? "(none)")}");
        Log($"DNS: {(primaryDns.Count > 0 ? string.Join(", ", primaryDns) : "(none)")}");

        Log("\n-- Connectivity Tests --");

        if (primaryGateway != null)
        {
            Log($"Ping Gateway ({primaryGateway}) : {PingHost(primaryGateway.ToString())}");
        }
        else
        {
            Log("Ping Gateway : (skipped - no gateway found)");
        }

        Log($"Ping 1.1.1.1 : {PingHost("1.1.1.1")}");

        Log("\n-- Active Adapters --");

        foreach (var x in activeAdapters)
        {
            var nic = x.Nic;
            var props = x.Props;

            Log($"\nAdapter: {nic.Name}");
            Log($"  Description: {nic.Description}");
            Log($"  Type: {nic.NetworkInterfaceType}");
            Log($"  Status: {nic.OperationalStatus}");
            Log($"  MAC: {FormatMac(nic.GetPhysicalAddress())}");

            if (nic.Speed > 0)
                Log($"  Link Speed: {nic.Speed / 1_000_000} Mbps");

            Log("  IP Addresses:");
            foreach (var ip in x.Unicast)
                Log($"    - {ip}");

            var gateways = props.GatewayAddresses
                .Select(g => g.Address)
                .Where(a => a != null && !IPAddress.IsLoopback(a))
                .ToList();

            if (gateways.Count > 0)
            {
                Log("  Gateways:");
                foreach (var gw in gateways)
                    Log($"    - {gw}");
            }

            // DNS servers
            var dns = props.DnsAddresses
                .Where(a => a != null && !IPAddress.IsLoopback(a))
                .ToList();

            if (dns.Count > 0)
            {
                Log("  DNS Servers:");
                foreach (var d in dns)
                    Log($"    - {d}");
            }
        }
    }

    static void PrintBatteryInfo(Action<string> Log)
    {
        Log("\n== Battery Info ==");

        var b = BatteryCollector.GetBatteryInfo();

        if (!b.IsPresent)
        {
            Log("No battery detected.");
            if (!string.IsNullOrWhiteSpace(b.Notes)) Log($"Notes: {b.Notes}");
            return;
        }

        if (b.Percentage.HasValue) Log($"Charge: {b.Percentage.Value}%");
        if (b.IsCharging.HasValue) Log($"Charging: {b.IsCharging.Value}");
        if (!string.IsNullOrWhiteSpace(b.Condition)) Log($"Condition: {b.Condition}");
        if (b.CycleCount.HasValue) Log($"Cycle Count: {b.CycleCount.Value}");
        if (b.FullChargeCapacitymAh.HasValue) Log($"Full Charge Capacity: {b.FullChargeCapacitymAh.Value} mAh");
        if (b.DesignCapacitymAh.HasValue) Log($"Design Capacity: {b.DesignCapacitymAh.Value} mAh");
    }

    static void PrintStorageInfo(Action<string> Log)
    {
        Log("\n== Storage (Physical Disks) ==");

        var s = SsdCollector.GetStorageInfo();

        if (!s.IsAvailable)
        {
            Log("Storage: (not available)");
            if (!string.IsNullOrWhiteSpace(s.Notes))
                Log($"Notes: {s.Notes}");
            return;
        }

        if (s.Disks.Count == 0)
        {
            Log("No disks detected.");
            return;
        }

        int i = 1;
        foreach (var d in s.Disks)
        {
            Log($"\nDisk {i++}:");

            if (!string.IsNullOrWhiteSpace(d.Name)) Log($"  Name: {d.Name}");
            if (!string.IsNullOrWhiteSpace(d.Model) && d.Model != d.Name) Log($"  Model: {d.Model}");

            if (d.CapacityBytes.HasValue)
                Log($"  Size: {d.CapacityBytes.Value / 1024 / 1024 / 1024} GB");
            else if (!string.IsNullOrWhiteSpace(d.CapacityText))
                Log($"  Size: {d.CapacityText}");

            if (d.IsSsd.HasValue)
                Log($"  Type: {(d.IsSsd.Value ? "SSD" : "HDD")}");

            if (!string.IsNullOrWhiteSpace(d.SmartStatus)) Log($"  SMART: {d.SmartStatus}");
            if (!string.IsNullOrWhiteSpace(d.TrimSupport)) Log($"  TRIM: {d.TrimSupport}");

            if (!string.IsNullOrWhiteSpace(d.InterfaceType)) Log($"  Interface: {d.InterfaceType}");
            if (!string.IsNullOrWhiteSpace(d.HealthStatus)) Log($"  Health: {d.HealthStatus}");
            if (!string.IsNullOrWhiteSpace(d.Serial)) Log($"  Serial: {d.Serial}");
        }
    }

    static void PrintHealthScore(Action<string> Log)
    {
        Log("\n== Health Score ==");

        var health = BuildHealthSummary();

        Log($"Overall: {health.OverallScore}/100 ({health.OverallLevel})");

        foreach (var item in health.Items)
        {
            Log($"- {item.Name}: {item.Level} ({item.Score}/100) - {item.Message}");
        }
    }



    static long ToGb(long bytes) => bytes / 1024 / 1024 / 1024;

    static string FormatMac(PhysicalAddress mac)
    {
        var bytes = mac.GetAddressBytes();
        if (bytes.Length == 0) return "(none)";
        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    static bool IsLinkLocal(IPAddress address)
    {
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal;

        return false;
    }

    static string PingHost(string host)
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(host, 1500);

            if (reply == null)
                return "No reply";

            return reply.Status == IPStatus.Success
                ? $"OK ({reply.RoundtripTime} ms)"
                : reply.Status.ToString();
        }
        catch (Exception ex)
        {
            return $"Error ({ex.GetType().Name})";
        }
    }
    static string GetCpuModel()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RunCommand("sysctl", "-n machdep.cpu.brand_string").Trim();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsWmiValue("Win32_Processor", "Name");
            }
        }
        catch { }

        return "";
    }

    public static long? GetTotalRamBytes()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var memBytes = RunCommand("sysctl", "-n hw.memsize").Trim();
                if (long.TryParse(memBytes, out long bytes))
                    return bytes;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var val = GetWindowsWmiValue("Win32_ComputerSystem", "TotalPhysicalMemory");
                if (long.TryParse(val, out long bytes))
                    return bytes;
            }
        }
        catch { }

        return null;
    }

    public static string RunCommand(string fileName, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        string output = process!.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return string.IsNullOrWhiteSpace(output) ? error : output;
    }

    static string GetWindowsWmiValue(string className, string propertyName)
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "";

            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT {propertyName} FROM {className}");

            using var results = searcher.Get();

            foreach (var obj in results)
            {
                var val = obj[propertyName];
                if (val != null)
                    return val.ToString() ?? "";
            }
        }
        catch { }

        return "";
    }

    static TimeSpan GetSystemUptime()
    {
        return TimeSpan.FromMilliseconds(Environment.TickCount64);
    }

    static string FormatUptime(TimeSpan uptime)
    {
        return $"{uptime.Days}d {uptime.Hours:D2}h {uptime.Minutes:D2}m {uptime.Seconds:D2}s";
    }
    static string GetLastBootTime(TimeSpan uptime)
    {
        var bootTime = DateTime.Now - uptime;
        return bootTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    static string GetDisplayUserName()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // When running with sudo, SUDO_USER is usually the original user
                var sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
                if (!string.IsNullOrWhiteSpace(sudoUser))
                    return sudoUser;
            }
        }
        catch { }

        return Environment.UserName;
    }

    static HealthItem ScoreCpu(double? cpuPct)
    {
        if (!cpuPct.HasValue) return Unknown("CPU", "CPU usage not available.");

        var cpu = cpuPct.Value;

        if (cpu < 70) return Green("CPU", 95, $"{cpu:F1}% usage (healthy)");
        if (cpu < 90) return Yellow("CPU", 70, $"{cpu:F1}% usage (high)");
        return Red("CPU", 35, $"{cpu:F1}% usage (very high)");
    }

    static HealthItem ScoreRam(double? ramPct)
    {
        if (!ramPct.HasValue) return Unknown("RAM", "RAM usage not available.");

        var ram = ramPct.Value;

        if (ram < 75) return Green("RAM", 95, $"{ram:F1}% used (healthy)");
        if (ram < 90) return Yellow("RAM", 65, $"{ram:F1}% used (tight)");
        return Red("RAM", 25, $"{ram:F1}% used (very tight)");
    }

    static HealthItem ScoreDiskFree()
    {
        try
        {
            DriveInfo? target = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                target = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase));
            else
                target = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name == "/");

            if (target == null) return Unknown("Disk", "Could not determine system drive.");

            var freePct = (double)target.AvailableFreeSpace / target.TotalSize * 100.0;

            if (freePct >= 20) return Green("Disk", 95, $"{freePct:F1}% free (healthy)");
            if (freePct >= 10) return Yellow("Disk", 60, $"{freePct:F1}% free (low)");
            return Red("Disk", 20, $"{freePct:F1}% free (very low)");
        }
        catch
        {
            return Unknown("Disk", "Disk score failed.");
        }
    }

    static HealthItem ScoreNetwork()
    {
        var internet = PingHost("1.1.1.1");
        var ok = internet.StartsWith("OK", StringComparison.OrdinalIgnoreCase);

        if (ok) return Green("Network", 95, $"Internet ping OK ({internet})");
        return Red("Network", 15, $"Internet ping failed ({internet})");
    }

    static HealthItem ScoreWifi(WifiInfo wifi)
    {
        if (wifi == null || !wifi.IsAvailable)
            return Unknown("Wi-Fi", "Wi-Fi not available / not detected.");

        if (wifi.SignalPercent.HasValue)
        {
            var s = wifi.SignalPercent.Value;
            if (s >= 70) return Green("Wi-Fi", 95, $"{wifi.Ssid ?? "(unknown)"} at {s}% (strong)");
            if (s >= 40) return Yellow("Wi-Fi", 65, $"{wifi.Ssid ?? "(unknown)"} at {s}% (ok)");
            return Red("Wi-Fi", 25, $"{wifi.Ssid ?? "(unknown)"} at {s}% (weak)");
        }

        if (wifi.RssiDbm.HasValue)
        {
            var r = wifi.RssiDbm.Value;
            if (r >= -67) return Green("Wi-Fi", 95, $"{wifi.Ssid ?? "(unknown)"} RSSI {r} dBm (strong)");
            if (r >= -75) return Yellow("Wi-Fi", 65, $"{wifi.Ssid ?? "(unknown)"} RSSI {r} dBm (ok)");
            return Red("Wi-Fi", 25, $"{wifi.Ssid ?? "(unknown)"} RSSI {r} dBm (weak)");
        }

        return Unknown("Wi-Fi", "Wi-Fi signal not available.");
    }

    static HealthItem ScoreBattery(BatteryInfo b)
    {
        if (b == null || !b.IsPresent)
            return Unknown("Battery", "No battery detected.");


        var cond = (b.Condition ?? "").Trim();

        if (cond.Equals("Normal", StringComparison.OrdinalIgnoreCase))
            return Green("Battery", 95, $"Condition: {cond}");

        if (!string.IsNullOrWhiteSpace(cond))
            return Yellow("Battery", 60, $"Condition: {cond}");
        return Unknown("Battery", "Battery condition not available.");
    }

    static HealthItem Green(string name, int score, string msg) =>
        new HealthItem { Name = name, Level = HealthLevel.Green, Score = score, Message = msg };

    static HealthItem Yellow(string name, int score, string msg) =>
        new HealthItem { Name = name, Level = HealthLevel.Yellow, Score = score, Message = msg };

    static HealthItem Red(string name, int score, string msg) =>
        new HealthItem { Name = name, Level = HealthLevel.Red, Score = score, Message = msg };

    static HealthItem Unknown(string name, string msg) =>
        new HealthItem { Name = name, Level = HealthLevel.Unknown, Score = 0, Message = msg };




}


