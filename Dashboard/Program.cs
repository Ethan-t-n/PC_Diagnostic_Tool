using System.Text.Json;

// Resolve paths relative to this project even when started from the repository root.
var dashboardRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = dashboardRoot
});
builder.WebHost.UseUrls("http://127.0.0.1:5000");
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

app.Run();
