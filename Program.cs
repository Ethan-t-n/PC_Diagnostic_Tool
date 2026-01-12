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

        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .OrderByDescending(n => n.OperationalStatus == OperationalStatus.Up)
            .ThenBy(n => n.NetworkInterfaceType.ToString())
            .ThenBy(n => n.Name)
            .ToList();

        if (adapters.Count == 0)
        {
            Console.WriteLine("No network adapters found.");
            return;
        }

        foreach (var nic in adapters)
        {

            var props = nic.GetIPProperties();

            Console.WriteLine($"\nAdapter: {nic.Name}");
            Console.WriteLine($"  Description: {nic.Description}");
            Console.WriteLine($"  Type: {nic.NetworkInterfaceType}");
            Console.WriteLine($"  Status: {nic.OperationalStatus}");
            Console.WriteLine($"  MAC: {FormatMac(nic.GetPhysicalAddress())}");

            if (nic.Speed > 0)
                Console.WriteLine($"  Link Speed: {nic.Speed / 1_000_000} Mbps");

            var unicast = props.UnicastAddresses
                .Select(u => u.Address)
                .Where(a => !IPAddress.IsLoopback(a))
                .ToList();

            if (unicast.Count > 0)
            {
                Console.WriteLine("  IP Addresses:");
                foreach (var ip in unicast)
                    Console.WriteLine($"    - {ip}");
            }
            else
            {
                Console.WriteLine("  IP Addresses: (none)");
            }

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
}
