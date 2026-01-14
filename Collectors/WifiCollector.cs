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
        var airportPath =
            "/System/Library/PrivateFrameworks/Apple80211.framework/Versions/Current/Resources/airport";

        var info = new Program.WifiInfo { Source = "macOS:airport -I" };

        try
        {
            var output = Program.RunCommand(airportPath, "-I");

            // If you're not on Wi-Fi, output may be sparse
            var ssid = FindValueAfterColon(output, "SSID");
            var rssiStr = FindValueAfterColon(output, "agrCtlRSSI");
            var noiseStr = FindValueAfterColon(output, "agrCtlNoise");

            info.IsAvailable = !string.IsNullOrWhiteSpace(ssid);

            info.Ssid = ssid;

            if (int.TryParse(rssiStr, out var rssi))
            {
                info.RssiDbm = rssi;
                info.SignalPercent = RssiToPercent(rssi);
            }

            if (int.TryParse(noiseStr, out var noise))
                info.NoiseDbm = noise;

            return info;
        }
        catch (Exception ex)
        {
            info.IsAvailable = false;
            info.Notes = $"macOS Wi-Fi query error: {ex.GetType().Name}";
            return info;
        }
    }

    // -------- Windows --------
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

            if (!string.IsNullOrWhiteSpace(ssid))
                info.IsAvailable = true;

            if (!string.IsNullOrWhiteSpace(signalStr))
            {
                // "87%" -> 87
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

    private static int RssiToPercent(int rssiDbm)
    {
        var pct = (rssiDbm + 100) * 2;
        if (pct < 0) pct = 0;
        if (pct > 100) pct = 100;
        return pct;
    }
}
