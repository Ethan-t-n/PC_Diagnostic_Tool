using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

public static class LiveMetrics
{
    public static async Task<object> SampleAsync(CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new { supported = false, reason = "Live CPU and memory sampling currently supports macOS only." };

        var topTask = RunAsync("top", new[] { "-l", "1", "-n", "0" }, cancellationToken);
        var pressureTask = RunAsync("memory_pressure", Array.Empty<string>(), cancellationToken);
        await Task.WhenAll(topTask, pressureTask);

        double? cpuUsage = null;
        var top = await topTask;
        var match = Regex.Match(top, @"CPU usage:.*?([\d.,]+)%\s*idle", RegexOptions.IgnoreCase);
        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var idle))
            cpuUsage = Math.Clamp(100 - idle, 0, 100);

        int? freeMemoryPressure = null;
        var pressure = await pressureTask;
        match = Regex.Match(pressure, @"System-wide memory free percentage:\s*(\d+)%", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var free))
            freeMemoryPressure = Math.Clamp(free, 0, 100);

        return new
        {
            supported = true,
            sampledAtUtc = DateTimeOffset.UtcNow,
            cpuUsagePercent = cpuUsage,
            macMemoryPressureFreePercent = freeMemoryPressure,
            // This is NOT the same as physical RAM free or RAM utilization.
            memoryMetricLabel = "macOS system-wide memory free percentage"
        };
    }

    private static async Task<string> RunAsync(string file, string[] arguments, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo(file)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            if (!process.Start()) return "";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                await errorTask;
                var output = await outputTask;
                return process.ExitCode == 0 ? output : "";
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                return "";
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or OperationCanceledException)
        {
            return "";
        }
    }
}
