# TS-SE Tool desktop preview

This is the Avalonia 12.2.0 native desktop app for the single combined Phase 2 rebuild. It targets .NET 10, uses MVVM and Microsoft dependency injection, and provides a shared dark/light Fluent theme.

## Run

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet run --project desktop/TSSE.Desktop/TSSE.Desktop.csproj
```

## Interface status

The app includes Command Center, Savegame Explorer, Vehicle Studio, Economy Intelligence, Savegame Time Machine, Compatibility Lab, Diagnostics Center, and Settings. Navigation search, Truck/Trailer/Coach preview selection, 7/30-day preview selection, and in-session theme switching are wired. The interface follows the operating-system UI language at startup, includes English and German text, and falls back to English for unsupported languages. The Settings page reports the resolved language.

These are interface previews. The app does not scan for profiles or open saves. It shows no fabricated player, vehicle, money, transaction, snapshot, or diagnostic data. Savegame writes and restore actions are absent. Latest .NET 10 matrix [45795444790](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45795444790) passed all seven jobs on `19e9fb9`: 98/98 tests on each of five desktop targets and 89/89 on each Fedora target, with no failures or skips; self-contained publish, package validation, native startup/shutdown, and Linux x64 screenshot smoke also passed. Explicit Apple Silicon run [45795444731](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45795444731) is queued for this source revision. The previous ARM64 run [45793859871](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45793859871) passed on `e5d97fa` before the early reader-line-limit change. Headless interaction tests cover navigation, search, theme, vehicle category, and analytics-window controls; headless screenshots for all pages were reviewed. The Xvfb image is platform-rendering evidence, not physical-desktop or accessibility review. Physical desktop/accessibility review, Windows MSI packaging, and signed packages remain open. See [Phase 2 status](../../docs/architecture/phase-2-foundation.md).
