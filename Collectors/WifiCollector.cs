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
        var info = new Program.WifiInfo { Source = "macOS:networksetup (+ airport if available)" };

        try
        {
            var ports = Program.RunCommand("networksetup", "-listallhardwareports");
            var wifiDevice = FindDeviceForHardwarePort(ports, "Wi-Fi")
                          ?? FindDeviceForHardwarePort(ports, "AirPort");

            info.InterfaceName = wifiDevice;

            if (string.IsNullOrWhiteSpace(wifiDevice))
            {
                info.IsAvailable = false;
                info.Notes = "Could not find Wi-Fi device (networksetup).";
                return info;
            }

            var ssidOut = Program.RunCommand("networksetup", $"-getairportnetwork {wifiDevice}");
            var ssid = ParseMacSsidFromNetworksetup(ssidOut);

            info.Ssid = ssid;
            info.IsAvailable = !string.IsNullOrWhiteSpace(ssid);

            TryFillRssiFromAirport(info);

            return info;
        }
        catch (Exception ex)
        {
            info.IsAvailable = false;
            info.Notes = $"macOS Wi-Fi query error: {ex.GetType().Name}";
            return info;
        }
    }

    private static string? FindDeviceForHardwarePort(string text, string portName)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("Hardware Port:", StringComparison.OrdinalIgnoreCase) &&
                lines[i].IndexOf(portName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                for (int j = i; j < Math.Min(i + 6, lines.Length); j++)
                {
                    if (lines[j].StartsWith("Device:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = lines[j].Split(':', 2);
                        if (parts.Length == 2) return parts[1].Trim();
                    }
                }
            }
        }

        return null;
    }

    private static string? ParseMacSsidFromNetworksetup(string text)
    {
        var idx = text.IndexOf("Current Wi-Fi Network:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var line = text.Substring(idx).Split('\n')[0].Trim();
        var parts = line.Split(':', 2);
        if (parts.Length < 2) return null;

        var value = parts[1].Trim();
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
        return value;
    }

    private static void TryFillRssiFromAirport(Program.WifiInfo info)
    {
        try
        {
            var airportPath =
                "/System/Library/PrivateFrameworks/Apple80211.framework/Versions/Current/Resources/airport";

            var output = Program.RunCommand(airportPath, "-I");

            var rssiStr = FindValueAfterColon(output, "agrCtlRSSI");
            var noiseStr = FindValueAfterColon(output, "agrCtlNoise");

            if (int.TryParse(rssiStr, out var rssi))
            {
                info.RssiDbm = rssi;
                info.SignalPercent = RssiToPercent(rssi);
            }

            if (int.TryParse(noiseStr, out var noise))
                info.NoiseDbm = noise;
        }
        catch
        {
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

            if (!string.IsNullOrWhiteSpace(ssid))
                info.IsAvailable = true;

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

    private static int RssiToPercent(int rssiDbm)
    {
        var pct = (rssiDbm + 100) * 2;
        if (pct < 0) pct = 0;
        if (pct > 100) pct = 100;
        return pct;
    }
}
