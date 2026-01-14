using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

public static class SsdCollector
{
    public static Program.StorageInfo GetStorageInfo()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetMacStorageInfo();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetWindowsStorageInfo();
        }
        catch { }

        return new Program.StorageInfo
        {
            IsAvailable = false,
            Notes = "Storage info not available on this platform."
        };
    }

    private static Program.StorageInfo GetMacStorageInfo()
    {
        var info = new Program.StorageInfo
        {
            IsAvailable = true,
            Source = "macOS:system_profiler(SPNVMeDataType+SPSerialATADataType)"
        };

        try
        {
            var nvme = Program.RunCommand("system_profiler", "SPNVMeDataType");
            ParseSystemProfilerDisks(nvme, info.Disks, assumeSsd: true);

            var sata = Program.RunCommand("system_profiler", "SPSerialATADataType");
            ParseSystemProfilerDisks(sata, info.Disks, assumeSsd: false);

            info.Disks = info.Disks
                .Where(d => !string.IsNullOrWhiteSpace(d.Name) || !string.IsNullOrWhiteSpace(d.Model))
                .GroupBy(d => (d.Name ?? "").Trim() + "|" + (d.Model ?? "").Trim())
                .Select(g => g.First())
                .ToList();

            if (info.Disks.Count == 0)
            {
                info.IsAvailable = false;
                info.Notes = "No physical disks found in system_profiler output.";
            }

            return info;
        }
        catch (Exception ex)
        {
            info.IsAvailable = false;
            info.Notes = $"macOS storage parse error: {ex.GetType().Name}";
            return info;
        }
    }

    private static void ParseSystemProfilerDisks(string text, List<Program.PhysicalDiskInfo> disks, bool assumeSsd)
    {
        Program.PhysicalDiskInfo? current = null;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            var trimmed = line.Trim();
            var isHeader =
                trimmed.EndsWith(":") &&
                !trimmed.Contains(": ") &&        
                trimmed.Length > 2 &&
                raw.StartsWith("      ");          

            if (isHeader)
            {
                if (current != null) disks.Add(current);

                current = new Program.PhysicalDiskInfo
                {
                    Name = trimmed.TrimEnd(':').Trim(),
                    IsSsd = assumeSsd
                };
                continue;
            }

            if (current == null) continue;

            var parts = trimmed.Split(':', 2);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var val = parts[1].Trim();

            if (key.Equals("Model", StringComparison.OrdinalIgnoreCase))
                current.Model = val;

            else if (key.Equals("Capacity", StringComparison.OrdinalIgnoreCase))
                current.CapacityText = val;

            else if (key.Equals("SMART Status", StringComparison.OrdinalIgnoreCase))
                current.SmartStatus = val;

            else if (key.Equals("TRIM Support", StringComparison.OrdinalIgnoreCase))
                current.TrimSupport = val;

            else if (key.Equals("Medium Type", StringComparison.OrdinalIgnoreCase))
            {
                current.MediumType = val;
                if (val.IndexOf("SSD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    val.IndexOf("Solid State", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    current.IsSsd = true;
                }
                else if (val.IndexOf("HDD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         val.IndexOf("Hard Disk", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    current.IsSsd = false;
                }
            }
        }

        if (current != null)
            disks.Add(current);
    }

    [SupportedOSPlatform("windows")]
    private static Program.StorageInfo GetWindowsStorageInfo()
    {
#pragma warning disable CA1416
        var info = new Program.StorageInfo
        {
            IsAvailable = true,
            Source = "Windows:WMI(MSFT_PhysicalDisk -> fallback Win32_DiskDrive)"
        };

        try
        {
            try
            {
                var scope = new System.Management.ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                scope.Connect();

                using var searcher = new System.Management.ManagementObjectSearcher(
                    scope,
                    new System.Management.ObjectQuery("SELECT FriendlyName, MediaType, Size, SerialNumber, HealthStatus FROM MSFT_PhysicalDisk")
                );

                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    var disk = new Program.PhysicalDiskInfo
                    {
                        Name = obj["FriendlyName"]?.ToString(),
                        Serial = obj["SerialNumber"]?.ToString(),
                        CapacityBytes = TryLong(obj["Size"]),
                        HealthStatus = obj["HealthStatus"]?.ToString()
                    };

                    var mediaType = TryInt(obj["MediaType"]);
                    disk.MediumType = mediaType?.ToString();

                    if (mediaType == 3) disk.IsSsd = true;
                    else if (mediaType == 4) disk.IsSsd = false;

                    info.Disks.Add(disk);
                }

                if (info.Disks.Count > 0)
                    return info;
            }
            catch
            {
            }

            using var dd = new System.Management.ManagementObjectSearcher(
                "SELECT Model, Size, SerialNumber, MediaType, InterfaceType FROM Win32_DiskDrive");

            foreach (System.Management.ManagementObject obj in dd.Get())
            {
                var disk = new Program.PhysicalDiskInfo
                {
                    Model = obj["Model"]?.ToString(),
                    Name = obj["Model"]?.ToString(),
                    Serial = obj["SerialNumber"]?.ToString(),
                    CapacityBytes = TryLong(obj["Size"]),
                    MediumType = obj["MediaType"]?.ToString(),
                    InterfaceType = obj["InterfaceType"]?.ToString()
                };

                if (!string.IsNullOrWhiteSpace(disk.MediumType) &&
                    disk.MediumType.IndexOf("SSD", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    disk.IsSsd = true;
                }

                info.Disks.Add(disk);
            }

            if (info.Disks.Count == 0)
            {
                info.IsAvailable = false;
                info.Notes = "No physical disks found via WMI.";
            }

            return info;
        }
        catch (Exception ex)
        {
            info.IsAvailable = false;
            info.Notes = $"Windows storage query error: {ex.GetType().Name}";
            return info;
        }
#pragma warning restore CA1416
    }

    private static long? TryLong(object? o)
        => o == null ? null : long.TryParse(o.ToString(), out var v) ? v : null;

    private static int? TryInt(object? o)
        => o == null ? null : int.TryParse(o.ToString(), out var v) ? v : null;
}
