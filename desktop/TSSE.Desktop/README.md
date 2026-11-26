# TS-SE Tool desktop preview

This is the Avalonia 12.2.0 native desktop app for the single combined Phase 2 rebuild. It targets .NET 10, uses MVVM and Microsoft dependency injection, and provides a shared dark/light Fluent theme.

## Run

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet run --project desktop/TSSE.Desktop/TSSE.Desktop.csproj
```

## Interface status

The app includes Command Center, Savegame Explorer, Vehicle Studio, Economy Intelligence, Savegame Time Machine, Compatibility Lab, Diagnostics Center, and Settings. Navigation search, Truck/Trailer/Coach preview selection, 7/30-day preview selection, and in-session theme switching are wired. The interface follows the operating-system UI language at startup, includes English and German text, and falls back to English for unsupported languages. The Settings page reports the resolved language.

These are interface previews. The app does not scan for profiles or open saves. It shows no fabricated player, vehicle, money, transaction, snapshot, or diagnostic data. Savegame writes and restore actions are absent. CI run [45726994656](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45726994656) on `98aa56b` passed the .NET 10 build, all 43 tests, self-contained publish, and native startup/shutdown on Linux x64/ARM64, Windows x64/ARM64, and macOS Intel. Linux DEB/RPM packages installed, launched, and removed successfully on Ubuntu 24.04 x64/ARM64; RPM dependency resolution on Fedora remains unverified. macOS ARM64 remains unvalidated pending a verified runner route. The app's Windows x64 window opened, but visual layout and interaction remain unverified. Windows/macOS installers and signed packages are not implemented; see [Phase 2 status](../../docs/architecture/phase-2-foundation.md).
