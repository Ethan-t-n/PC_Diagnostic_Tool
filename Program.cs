using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.Text.Json;



class Program
{
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
        PrintNetworkInfo(Log);

        Log();
        Log("Scan complete.");

        ExportReports(report.ToString());
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
            var jsonObj = new
            {
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                machineName = Environment.MachineName,
                report = textReport
            };

            var json = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
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
        Log($"User: {Environment.UserName}");
        Log($"CPU Cores: {Environment.ProcessorCount}");

        var cpuModel = GetCpuModel();
        if (!string.IsNullOrWhiteSpace(cpuModel))
            Log($"CPU Model: {cpuModel}");

        var ramBytes = GetTotalRamBytes();
        if (ramBytes.HasValue)
            Log($"RAM: {ramBytes.Value / 1024 / 1024 / 1024} GB");

        Log($".NET Version: {Environment.Version}");

        var uptime = GetSystemUptime();
        Log($"Uptime: {FormatUptime(uptime)}");
        Log($"Last Boot: {GetLastBootTime(uptime)}");

        Log($".NET Version: {Environment.Version}");

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

    static long? GetTotalRamBytes()
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

    static string RunCommand(string fileName, string args)
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
        // Milliseconds since system start
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


}


