using System;
using System.Linq;
using System.Runtime.InteropServices;

public static class WifiCollector
{
    public static Program.WifiInfo GetWifiInfo()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetMacWifiInfo();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetWindowsWifiInfo();
        }
        catch { }

        return new Program.WifiInfo
        {
            IsAvailable = false,
            Notes = "Wi-Fi info not available on this platform."
        };
    }

    private static Program.WifiInfo GetMacWifiInfo()
    {
        var info = new Program.WifiInfo { Source = "macOS:wdutil info" };

        try
        {
            var output = Program.RunCommand("wdutil", "info");

            if (output.IndexOf("usage:", StringComparison.OrdinalIgnoreCase) >= 0 &&
                output.IndexOf("sudo wdutil info", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                info.IsAvailable = false;
                info.Notes = "macOS requires admin privileges for Wi-Fi details. Run: sudo dotnet run";
                return info;
            }

            var wifiSection = ExtractSection(output, "WIFI");

            var iface = FindValueAfterColon(wifiSection, "Interface Name");
            var ssid = FindValueAfterColon(wifiSection, "SSID");
            var rssiStr = FindValueAfterColon(wifiSection, "RSSI");     
            var noiseStr = FindValueAfterColon(wifiSection, "Noise");   
            var txRateStr = FindValueAfterColon(wifiSection, "Tx Rate");
            var channel = FindValueAfterColon(wifiSection, "Channel");
            var phy = FindValueAfterColon(wifiSection, "PHY Mode");
            var security = FindValueAfterColon(wifiSection, "Security");

            info.InterfaceName = iface;
            info.Ssid = ssid;
            info.Channel = channel;
            info.PhyMode = phy;
            info.Security = security;

            info.IsAvailable = !string.IsNullOrWhiteSpace(ssid);

            if (int.TryParse(OnlyInt(rssiStr), out var rssi))
            {
                info.RssiDbm = rssi;
                info.SignalPercent = RssiToPercent(rssi);
            }

            if (int.TryParse(OnlyInt(noiseStr), out var noise))
                info.NoiseDbm = noise;

            if (double.TryParse(OnlyDouble(txRateStr), out var tx))
                info.TxRateMbps = tx;

            if (!info.IsAvailable)
                info.Notes = "Not connected to Wi-Fi (SSID missing).";

            return info;
        }
        catch (Exception ex)
        {
            info.IsAvailable = false;
            info.Notes = $"macOS Wi-Fi query error: {ex.GetType().Name}";
            return info;
        }
    }

    private static Program.WifiInfo GetWindowsWifiInfo()
    {
        var info = new Program.WifiInfo { Source = "Windows:netsh wlan show interfaces" };

        try
        {
            var output = Program.RunCommand("netsh", "wlan show interfaces");

            if (output.IndexOf("no wireless interface", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                info.IsAvailable = false;
                info.Notes = "No wireless interface found.";
                return info;
            }

            var ssid = FindValueAfterColon(output, "SSID");
            var signalStr = FindValueAfterColon(output, "Signal");
            var name = FindValueAfterColon(output, "Name");

            info.InterfaceName = name;
            info.Ssid = ssid;

            info.IsAvailable = !string.IsNullOrWhiteSpace(ssid);

            if (!string.IsNullOrWhiteSpace(signalStr))
            {
                var digits = new string(signalStr.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var pct))
                    info.SignalPercent = pct;
            }

            return info;
        }
        catch (Exception ex)
        {
            info.IsAvailable = false;
            info.Notes = $"Windows Wi-Fi query error: {ex.GetType().Name}";
            return info;
        }
    }

    private static string? FindValueAfterColon(string text, string label)
    {
        var lines = text.Split('\n');

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                    return parts[1].Trim();
            }
        }

        return null;
    }

    private static string ExtractSection(string text, string sectionTitle)
    {
        var lines = text.Split('\n');
        int start = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Equals(sectionTitle, StringComparison.OrdinalIgnoreCase))
            {
                start = i;
                break;
            }
        }

        if (start < 0) return text;

        var collected = lines.Skip(start).ToList();

        for (int i = 3; i < collected.Count; i++)
        {
            var t = collected[i].Trim();
            if (t.Length > 10 && t.All(c => c == '—' || c == '-' || c == '='))
            {
                collected = collected.Take(i).ToList();
                break;
            }
        }

        return string.Join("\n", collected);
    }

    private static string OnlyInt(string? s)
        => string.IsNullOrWhiteSpace(s) ? "" : new string(s.Where(c => char.IsDigit(c) || c == '-').ToArray());

    private static string OnlyDouble(string? s)
        => string.IsNullOrWhiteSpace(s) ? "" : new string(s.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());

    private static int RssiToPercent(int rssiDbm)
    {
        var pct = (rssiDbm + 100) * 2;
        if (pct < 0) pct = 0;
        if (pct > 100) pct = 100;
        return pct;
    }
}
