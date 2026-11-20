using System.Diagnostics;

public static class DiagnosticScan
{
    public static async Task<(bool Success, string Message)> RunAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        var project = Path.Combine(repositoryRoot, "PC_Diagnostic_Tool.csproj");
        if (!File.Exists(project)) return (false, "Diagnostic console project was not found.");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("run");
        process.StartInfo.ArgumentList.Add("--project");
        process.StartInfo.ArgumentList.Add(project);
        try
        {
            if (!process.Start()) return (false, "Unable to start the diagnostic scan.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                var output = await outputTask;
                var error = await errorTask;
                if (process.ExitCode != 0)
                    return (false, "Scan failed: " + ShortMessage(error));
                if (!output.Contains("Scan complete.", StringComparison.Ordinal))
                    return (false, "Scan finished without confirming completion.");
                return (true, "Scan completed.");
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                return (false, "Scan cancelled or exceeded the 90-second timeout.");
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return (false, "Unable to run diagnostic CLI: " + ex.Message);
        }
    }

    private static string ShortMessage(string message) =>
        string.IsNullOrWhiteSpace(message) ? "See the server logs." :
        message.Length > 240 ? message[..240] : message;
}
