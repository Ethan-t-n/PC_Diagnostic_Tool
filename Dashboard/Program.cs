using System.Text.Json;

// Resolve paths relative to this project even when started from the repository root.
var dashboardRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = dashboardRoot
});
builder.WebHost.UseUrls("http://127.0.0.1:5050");
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

var reportsDirectory = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "reports"));

app.MapGet("/api/report/latest", async () =>
{
    if (!Directory.Exists(reportsDirectory))
        return Results.NotFound(new { error = "No reports found. Run dotnet run --project PC_Diagnostic_Tool.csproj from the repository root first." });

    var latest = Directory.EnumerateFiles(reportsDirectory, "pc_diagnostic_*.json")
        .OrderByDescending(Path.GetFileName)
        .FirstOrDefault();

    if (latest is null)
        return Results.NotFound(new { error = "No JSON reports found. Run a diagnostic scan first." });

    try
    {
        await using var stream = File.OpenRead(latest);
        using var document = await JsonDocument.ParseAsync(stream);
        return Results.Json(new
        {
            report = document.RootElement.Clone(),
            file = Path.GetFileName(latest),
            lastModifiedUtc = File.GetLastWriteTimeUtc(latest)
        });
    }
    catch (JsonException)
    {
        return Results.Problem("The latest diagnostic report is not valid JSON.", statusCode: 500);
    }
    catch (IOException)
    {
        return Results.Problem("Could not read the latest diagnostic report.", statusCode: 500);
    }
});

// Serialize live probes so multiple browser tabs cannot spawn unbounded processes.
using var liveProbeGate = new SemaphoreSlim(1, 1);
app.MapGet("/api/live", async (CancellationToken cancellationToken) =>
{
    if (!await liveProbeGate.WaitAsync(0, cancellationToken))
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    try
    {
        return Results.Json(await LiveMetrics.SampleAsync(cancellationToken));
    }
    finally
    {
        liveProbeGate.Release();
    }
});

// Scans are only accessible from this loopback-bound server. Prevent concurrent CLI runs.
using var scanGate = new SemaphoreSlim(1, 1);
app.MapPost("/api/scan", async (CancellationToken cancellationToken) =>
{
    if (!await scanGate.WaitAsync(0, cancellationToken))
        return Results.Conflict(new { error = "A diagnostic scan is already running." });
    try
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, ".."));
        var result = await DiagnosticScan.RunAsync(repositoryRoot, cancellationToken);
        return result.Success
            ? Results.Ok(new { message = result.Message })
            : Results.Problem(result.Message, statusCode: 500);
    }
    finally
    {
        scanGate.Release();
    }
});

app.Run();
