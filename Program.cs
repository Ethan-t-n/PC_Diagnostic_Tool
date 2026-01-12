using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

class Program
{
    static void Main()
    {
        Console.WriteLine("PC Diagnostic Tool v0.1");
        Console.WriteLine("------------------------");

        PrintBasicSystemInfo();
        PrintDiskInfo();
        PrintNetworkInfo();

        Console.WriteLine("\nScan complete.");
    }

    static void PrintBasicSystemInfo()
    {
        Console.WriteLine("\n== System Info ==");

        Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Architecture: {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"Machine Name: {Environment.MachineName}");
        Console.WriteLine($"User: {Environment.UserName}");
        Console.WriteLine($"CPU Cores: {Environment.ProcessorCount}");
        Console.WriteLine($".NET Version: {Environment.Version}");
    }

    static void PrintDiskInfo()
    {
        Console.WriteLine("\n== Disk Info ==");

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;

            Console.WriteLine($"Drive: {drive.Name}");
            Console.WriteLine($"  Format: {drive.DriveFormat}");
            Console.WriteLine($"  Total: {ToGb(drive.TotalSize)} GB");
            Console.WriteLine($"  Free:  {ToGb(drive.AvailableFreeSpace)} GB");
        }
    }

    static void PrintNetworkInfo()
    {
        Console.WriteLine("\n== Network Info ==");

        Console.WriteLine($"Host: {Dns.GetHostName()}");

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
            Console.WriteLine("No network adapters found.");
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

        Console.WriteLine("\n-- Network Summary --");
        Console.WriteLine($"Primary Adapter: {primary.Nic.Name} ({primary.Nic.NetworkInterfaceType})");
        Console.WriteLine($"IPv4: {(bestIPv4?.ToString() ?? "(none)")}");
        Console.WriteLine($"IPv6: {(bestIPv6?.ToString() ?? "(none)")}");
        Console.WriteLine($"Gateway: {(primaryGateway?.ToString() ?? "(none)")}");
        Console.WriteLine($"DNS: {(primaryDns.Count > 0 ? string.Join(", ", primaryDns) : "(none)")}");

        Console.WriteLine("\n-- Connectivity Tests --");

        if (primaryGateway != null)
        {
            Console.WriteLine($"Ping Gateway ({primaryGateway}) : {PingHost(primaryGateway.ToString())}");
        }
        else
        {
            Console.WriteLine("Ping Gateway : (skipped - no gateway found)");
        }

        Console.WriteLine($"Ping 1.1.1.1 : {PingHost("1.1.1.1")}");

        Console.WriteLine("\n-- Active Adapters --");

        foreach (var x in activeAdapters)
        {
            var nic = x.Nic;
            var props = x.Props;

            Console.WriteLine($"\nAdapter: {nic.Name}");
            Console.WriteLine($"  Description: {nic.Description}");
            Console.WriteLine($"  Type: {nic.NetworkInterfaceType}");
            Console.WriteLine($"  Status: {nic.OperationalStatus}");
            Console.WriteLine($"  MAC: {FormatMac(nic.GetPhysicalAddress())}");

            if (nic.Speed > 0)
                Console.WriteLine($"  Link Speed: {nic.Speed / 1_000_000} Mbps");

            Console.WriteLine("  IP Addresses:");
            foreach (var ip in x.Unicast)
                Console.WriteLine($"    - {ip}");

            var gateways = props.GatewayAddresses
                .Select(g => g.Address)
                .Where(a => a != null && !IPAddress.IsLoopback(a))
                .ToList();

            if (gateways.Count > 0)
            {
                Console.WriteLine("  Gateways:");
                foreach (var gw in gateways)
                    Console.WriteLine($"    - {gw}");
            }

            // DNS servers
            var dns = props.DnsAddresses
                .Where(a => a != null && !IPAddress.IsLoopback(a))
                .ToList();

            if (dns.Count > 0)
            {
                Console.WriteLine("  DNS Servers:");
                foreach (var d in dns)
                    Console.WriteLine($"    - {d}");
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
}

