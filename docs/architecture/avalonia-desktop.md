# Avalonia desktop architecture

## Technology choices

- **UI and desktop host:** Avalonia 12.2.0 on .NET 10. The same native UI project targets Windows, Linux, and macOS without a browser runtime.
- **Presentation pattern:** MVVM with `INotifyPropertyChanged`, commands, a navigation service, and page view models. UI state is separate from the savegame parser.
- **Dependency injection:** `Microsoft.Extensions.DependencyInjection` owns the foundation status, compatibility registry, navigation, theme, and main-window view models.
- **Theme:** one Fluent control theme and central theme dictionaries with dark and light palettes.
- **UI tests:** Avalonia headless plus xUnit v3 for a control-tree/window-load smoke test; regular unit tests cover view-model state, filtering, selected-page navigation, theme delegation, and preview controls.

Avalonia's current desktop platform matrix includes Windows x64/ARM64, Linux x64/ARM64 across listed distributions, and macOS x64/ARM64. Linux uses X11 directly; Wayland is available through an opt-in backend package. These are framework capabilities, not verified build or runtime results for this application. See the [official supported-platform matrix](https://docs.avaloniaui.net/docs/supported-platforms), [DI/MVVM guide](https://docs.avaloniaui.net/docs/app-development/dependency-injection), and [headless xUnit guide](https://docs.avaloniaui.net/docs/testing/headless-xunit).

## Application composition

`Program` starts Avalonia's classic desktop lifetime. `App` registers the current empty `GameAdapterRegistry`, a status service, theme service, page catalog, navigation service and main-window view model in the DI container. The main window owns a searchable navigation rail, tracks the selected page, highlights it, and exposes each section's accessible name and help text. A content presenter displays the current page. The section view displays facts from the foundation service alongside explicit preview/empty states.

The window can resize in the desktop layout; cards wrap inside a scrolling content area. The 7/30-day and Truck/Trailer/Coach selectors only update preview state. They do not query game data or claim those editors are implemented.

## Data and safety boundaries

- No game folder discovery, background watcher, file-picker, profile loader or save read is wired to the app.
- No save write, backup restore, quarantine, or process-control command exists in the UI.
- The status cards use `GameAdapterRegistry.RegistrationCount`; the registry starts empty.
- Account values, vehicles, transactions, snapshots and diagnoses are not populated with invented examples.
- A future reader must be version-pinned and read-only first. It must pass real-fixture roundtrips and compatibility review before becoming discoverable from the UI.

## Build and run

From the repository root with the .NET 10 SDK:

```powershell
dotnet test TSSE.NextGen.sln --configuration Release
dotnet run --project desktop/TSSE.Desktop/TSSE.Desktop.csproj
```

The current repository CI additionally publishes self-contained app outputs for the six x64/ARM64 RIDs. No installer or signed distributable is generated in this phase.

## Distribution notes

- **Windows:** Avalonia's Win32 backend; publish separate x64 and ARM64 bundles. Package as MSI/WiX only after clean-machine installation and upgrade checks.
- **Linux:** Avalonia X11 backend with distro dependencies including X11, fontconfig and Skia-related native libraries. Test DEB and RPM separately; decide Wayland support and AppImage only after platform testing.
- **macOS:** Avalonia's native Objective-C++ backend. Publish Intel and Apple Silicon bundles, then test code signing, hardened runtime, notarization and DMG installation.

Self-contained .NET output removes the requirement for a separately installed .NET runtime, but it does not bundle every operating-system native library. Packaging must declare or include the required platform libraries and be validated on clean systems.
