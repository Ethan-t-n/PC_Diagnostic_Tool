# Local Health Dashboard (first version)

A read-only, dark-themed ASP.NET Core dashboard for the latest **real JSON report** produced by the existing C# CLI, plus live macOS CPU and memory-pressure measurements. It runs on the Mac itself and binds only to `127.0.0.1` (not your LAN or the internet).

## Start on macOS

From the repository root:

```bash
dotnet run --project PC_Diagnostic_Tool.csproj
dotnet run --project Dashboard/Dashboard.csproj
```

Leave the second command running and open **http://127.0.0.1:5000** in a browser. Press Ctrl+C to stop the local web server.

The first command produces a fresh JSON report in `reports/`; the dashboard loads the most recent `pc_diagnostic_*.json` file. Click **Run new scan** to invoke the existing CLI from the dashboard, or leave the tab visible for automatic full scans every **5 minutes**. Each successful scan writes fresh JSON/TXT reports in `reports/`, then reloads the dashboard's health scores, disk, battery and network details. The **Reload report** button only re-reads the newest report; it does not initiate a scan.

**Storage note:** full scans create new files in `reports/`. They are not automatically deleted. On low-storage Macs, keep an eye on the report folder. Automatic scans pause while the browser tab is hidden. The CLI is invoked through a localhost-only endpoint with a 90-second timeout and a single-scan concurrency guard.

## Scope and limitations

- Displays existing health scores, storage, battery, networking and system information from the CLI.
- **Live graphs** show CPU utilization and macOS `memory_pressure` system-wide free memory percentage every 10 seconds while the tab is visible. The latter is not physical RAM free or RAM usage. History lives only in the browser tab (30 samples); it is not persisted.
- The overall health score, component health bars, storage, battery and network details are **snapshots** from the latest full CLI scan; they refresh after a manual or five-minute automatic full scan, not on every 10-second graph update.
- If macOS blocks a measurement, the graph displays N/A rather than inventing a value.
- Live sampling currently supports macOS only; other platforms retain the snapshot dashboard.
- No GPU, temperatures, or fan values are invented.
- No authentication is included because the server is deliberately bound to localhost only. Do not change the binding to `0.0.0.0` without adding access controls.
- `reports/` is ignored by Git; report files stay local.
- The existing console application is unchanged. The root project excludes dashboard C# sources from its compilation.

## Future improvements

Share a single collection service between CLI and web app, add live battery and network measurements, persist historical samples, then build richer component details.
