using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

public static class PerformanceCollector
{
    public static (double? cpuPercent, double? ramPercent) GetCpuAndRamUsage()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetWindowsCpuAndRam();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetMacCpuAndRam();
        }
        catch { }

        return (null, null);
    }

    [SupportedOSPlatform("windows")]
    private static (double? cpuPercent, double? ramPercent) GetWindowsCpuAndRam()
    {
#pragma warning disable CA1416
        try
        {
            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            using var ramAvailMb = new PerformanceCounter("Memory", "Available MBytes");

            _ = cpuCounter.NextValue();
            System.Threading.Thread.Sleep(1000);
            var cpu = cpuCounter.NextValue();

            var totalBytes = Program_GetTotalRamBytesSafe();
            if (totalBytes == null) return (cpu, null);

            var availableBytes = (long)(ramAvailMb.NextValue() * 1024 * 1024);
            var usedBytes = Math.Max(0, totalBytes.Value - availableBytes);
            var ramPercent = (double)usedBytes / totalBytes.Value * 100.0;

            return (cpu, ramPercent);
        }
        catch
        {
            return (null, null);
        }
#pragma warning restore CA1416
    }

    private static (double? cpuPercent, double? ramPercent) GetMacCpuAndRam()
    {
        try
        {
            var topOut = Program_RunCommandSafe("top", "-l 1 -n 0");
            var cpu = ParseMacCpuPercent(topOut);

            var totalBytes = Program_GetTotalRamBytesSafe();
            if (totalBytes == null) return (cpu, null);

            var vmOut = Program_RunCommandSafe("vm_stat", "");
            var usedBytes = ParseMacUsedMemoryBytes(vmOut);
            if (usedBytes == null) return (cpu, null);

            var ramPercent = (double)usedBytes.Value / totalBytes.Value * 100.0;
            return (cpu, ramPercent);
        }
        catch
        {
            return (null, null);
        }
    }

    private static double? ParseMacCpuPercent(string text)
    {
        var idx = text.IndexOf("CPU usage:");
        if (idx < 0) return null;

        var line = text.Substring(idx).Split('\n')[0];
        var idleIdx = line.IndexOf("idle");
        if (idleIdx < 0) return null;

        var percentIdx = line.LastIndexOf('%', idleIdx);
        if (percentIdx < 0) return null;

        int start = percentIdx - 1;
        while (start >= 0 && (char.IsDigit(line[start]) || line[start] == '.')) start--;
        var idleStr = line.Substring(start + 1, percentIdx - (start + 1));

        if (!double.TryParse(idleStr, out var idle)) return null;
        return Math.Max(0, 100.0 - idle);
    }

    private static long? ParseMacUsedMemoryBytes(string text)
    {
        long pageSize = 4096;
        var psIdx = text.IndexOf("page size of");
        if (psIdx >= 0)
        {
            var psLine = text.Substring(psIdx).Split('\n')[0];
            var parts = psLine.Split(' ');
            foreach (var p in parts)
            {
                if (long.TryParse(p, out var v))
                {
                    pageSize = v;
                    break;
                }
            }
        }

        long GetPages(string key)
        {
            var k = key + ":";
            var i = text.IndexOf(k);
            if (i < 0) return 0;
            var line = text.Substring(i).Split('\n')[0];
            var nums = new string(line.Where(c => char.IsDigit(c)).ToArray());
            return long.TryParse(nums, out var val) ? val : 0;
        }

        var active = GetPages("Pages active");
        var inactive = GetPages("Pages inactive");
        var wired = GetPages("Pages wired down");
        var compressed = GetPages("Pages occupied by compressor");

        var usedPages = active + inactive + wired + compressed;
        return usedPages * pageSize;
    }

    private static string Program_RunCommandSafe(string file, string args) => Program.RunCommand(file, args);
    private static long? Program_GetTotalRamBytesSafe() => Program.GetTotalRamBytes();
}
