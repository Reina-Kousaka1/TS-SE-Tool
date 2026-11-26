# TS-SE Tool desktop preview

This is the Avalonia 12.2.0 native desktop app for the single combined Phase 2 rebuild. It targets .NET 10, uses MVVM and Microsoft dependency injection, and provides a shared dark/light Fluent theme.

## Run

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet run --project desktop/TSSE.Desktop/TSSE.Desktop.csproj
```

## Interface status

The app includes Command Center, Savegame Explorer, Vehicle Studio, Economy Intelligence, Savegame Time Machine, Compatibility Lab, Diagnostics Center, and Settings. Navigation search, Truck/Trailer/Coach preview selection, 7/30-day preview selection, and in-session theme switching are wired. The interface follows the operating-system UI language at startup, includes English and German text, and falls back to English for unsupported languages. The Settings page reports the resolved language.

These are interface previews. The app does not scan for profiles or open saves. It shows no fabricated player, vehicle, money, transaction, snapshot, or diagnostic data. Savegame writes and restore actions are absent. Full .NET 10 matrix [45776930808](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45776930808) passed all seven jobs: 80/80 tests on the five desktop targets and 71/71 on both Fedora targets, with self-contained publish, package validation, and native startup/shutdown. The Linux x64 job also captured the native Avalonia window under Xvfb and verified a 1360×900 PNG with 5,496 colors. Explicit Apple Silicon validation [45776930823](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45776930823) passed 80/80 tests, app bundle/DMG validation, and startup/shutdown. Headless interaction tests cover navigation, search, theme, vehicle category, and analytics-window controls; headless screenshots for all pages were reviewed. The Xvfb image is platform-rendering evidence, not physical-desktop or accessibility review. Physical desktop/accessibility review, Windows MSI packaging, and signed packages remain open. See [Phase 2 status](../../docs/architecture/phase-2-foundation.md).
