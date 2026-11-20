# Local Health Dashboard (first version)

A read-only, dark-themed ASP.NET Core dashboard for the latest **real JSON report** produced by the existing C# CLI, plus live macOS CPU and memory-pressure measurements. It runs on the Mac itself and binds only to `127.0.0.1` (not your LAN or the internet).

## Start on macOS

From the repository root:

```bash
dotnet run --project PC_Diagnostic_Tool.csproj
dotnet run --project Dashboard/Dashboard.csproj
```

Leave the second command running and open **http://127.0.0.1:5000** in a browser. Press Ctrl+C to stop the local web server.

The first command produces a fresh JSON report in `reports/`; the dashboard loads the most recent `pc_diagnostic_*.json` file. To refresh the measurements, run the first command again in another terminal and then press **Reload report** in the browser. The reload button reads the latest report; it does **not** run a new diagnostic scan.

## Scope and limitations

- Displays existing health scores, storage, battery, networking and system information from the CLI.
- **Live graphs** show CPU utilization and macOS `memory_pressure` system-wide free memory percentage every 10 seconds while the tab is visible. The latter is not physical RAM free or RAM usage. History lives only in the browser tab (30 samples); it is not persisted.
- The overall health score, component health bars, storage, battery and network details are **snapshots** from the most recent CLI report; live graph updates do not update those health scores.
- If macOS blocks a measurement, the graph displays N/A rather than inventing a value.
- Live sampling currently supports macOS only; other platforms retain the snapshot dashboard.
- No GPU, temperatures, or fan values are invented.
- No authentication is included because the server is deliberately bound to localhost only. Do not change the binding to `0.0.0.0` without adding access controls.
- `reports/` is ignored by Git; report files stay local.
- The existing console application is unchanged. The root project excludes dashboard C# sources from its compilation.

## Future improvements

Share a single collection service between CLI and web app, add live battery and network measurements, persist historical samples, then build richer component details.
