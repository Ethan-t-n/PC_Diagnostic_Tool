using System.Diagnostics;

public static class ProcessMetrics
{
    public static async Task<object> SampleAsync(CancellationToken cancellationToken)
    {
        var before = Snapshot();
        await Task.Delay(800, cancellationToken);
        var after = Snapshot();
        var rows = new List<object>();
        foreach (var item in after)
        {
            if (!before.TryGetValue(item.Key, out var previous)) continue;
            var current = item.Value;
            if (current.StartTicks != previous.StartTicks) continue;
            var elapsed = (current.SampleTicks - previous.SampleTicks) / (double)Stopwatch.Frequency;
            if (elapsed <= 0) continue;
            var cpu = (current.CpuTicks - previous.CpuTicks) / (double)TimeSpan.TicksPerSecond / elapsed / Environment.ProcessorCount * 100;
            rows.Add(new { pid = item.Key, name = current.Name, cpuPercent = Math.Round(Math.Clamp(cpu, 0, 100), 1), memoryBytes = current.MemoryBytes });
        }
        return new { sampledAtUtc = DateTimeOffset.UtcNow, cpuDefinition = "Percent of total logical CPU capacity", processes = rows };
    }

    private static Dictionary<int, Sample> Snapshot()
    {
        var result = new Dictionary<int, Sample>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.HasExited) continue;
                    var cpu = process.TotalProcessorTime.Ticks;
                    var start = process.StartTime.ToUniversalTime().Ticks;
                    var memory = process.WorkingSet64;
                    result[process.Id] = new Sample(process.ProcessName, cpu, start, memory, Stopwatch.GetTimestamp());
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Other users' and protected processes may be inaccessible.
                }
            }
        }
        return result;
    }

    private sealed record Sample(string Name, long CpuTicks, long StartTicks, long MemoryBytes, long SampleTicks);
}
