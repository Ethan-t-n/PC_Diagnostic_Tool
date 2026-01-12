using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

public static class BatteryCollector
{
    public static Program.BatteryInfo GetBatteryInfo()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetMacBatteryInfo();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetWindowsBatteryInfo();
        }
        catch { }

        return new Program.BatteryInfo
        {
            IsPresent = false,
            Notes = "Battery info not available on this platform.",
            Source = "unknown"
        };
    }

    private static Program.BatteryInfo GetMacBatteryInfo()
    {
        var info = new Program.BatteryInfo { Source = "macOS:pmset+system_profiler" };

        try
        {
            var pmset = Program.RunCommand("pmset", "-g batt");

            info.IsPresent = pmset.Contains("InternalBattery", StringComparison.OrdinalIgnoreCase);

            if (!info.IsPresent)
            {
                info.Notes = "No InternalBattery found (desktop or no battery).";
                return info;
            }

            var percentToken = pmset.Split(' ', '\n', '\r', '\t')
                .FirstOrDefault(t => t.EndsWith("%;", StringComparison.Ordinal));

            if (percentToken != null)
            {
                var pctStr = percentToken.Replace("%;", "");
                if (int.TryParse(pctStr, out var pct))
                    info.Percentage = pct;
            }

            if (pmset.Contains("charging", StringComparison.OrdinalIgnoreCase))
                info.IsCharging = true;
            else if (pmset.Contains("discharging", StringComparison.OrdinalIgnoreCase))
                info.IsCharging = false;
            else if (pmset.Contains("charged", StringComparison.OrdinalIgnoreCase))
                info.IsCharging = false;

            var sp = Program.RunCommand("system_profiler", "SPPowerDataType");

            info.CycleCount = TryParseIntAfterLabel(sp, "Cycle Count");
            info.Condition = TryParseStringAfterLabel(sp, "Condition");
            info.FullChargeCapacitymAh = TryParseIntAfterLabel(sp, "Full Charge Capacity");
            info.DesignCapacitymAh = TryParseIntAfterLabel(sp, "Design Capacity");

            return info;
        }
        catch (Exception ex)
        {
            info.IsPresent = false;
            info.Notes = $"macOS battery parse error: {ex.GetType().Name}";
            return info;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Program.BatteryInfo GetWindowsBatteryInfo()
    {
#pragma warning disable CA1416
        var info = new Program.BatteryInfo { Source = "Windows:WMI(Win32_Battery)" };

        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery");

            using var results = searcher.Get();

            var first = results.Cast<System.Management.ManagementObject>().FirstOrDefault();
            if (first == null)
            {
                info.IsPresent = false;
                info.Notes = "Win32_Battery returned no results (desktop or no battery).";
                return info;
            }

            info.IsPresent = true;

            var pctObj = first["EstimatedChargeRemaining"];
            if (pctObj != null && int.TryParse(pctObj.ToString(), out var pct))
                info.Percentage = pct;

            var statusObj = first["BatteryStatus"];
            if (statusObj != null && int.TryParse(statusObj.ToString(), out var status))
            {
                info.IsCharging = status == 2 || status == 6;
                if (status == 3) info.IsCharging = false;
            }

            return info;
        }
        catch (Exception ex)
        {
            info.IsPresent = false;
            info.Notes = $"Windows battery query error: {ex.GetType().Name}";
            return info;
        }
#pragma warning restore CA1416
    }
    private static int? TryParseIntAfterLabel(string text, string label)
    {
        var idx = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var line = text.Substring(idx).Split('\n')[0];
        var digits = new string(line.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var val) ? val : null;
    }

    private static string? TryParseStringAfterLabel(string text, string label)
    {
        var idx = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var line = text.Substring(idx).Split('\n')[0];
        var parts = line.Split(':');
        if (parts.Length < 2) return null;

        return parts[1].Trim();
    }
}

