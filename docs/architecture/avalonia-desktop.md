# Avalonia desktop architecture

## Technology choices

- **UI and desktop host:** Avalonia 12.2.0 on .NET 10. The same native UI project targets Windows, Linux, and macOS without a browser runtime.
- **Presentation pattern:** MVVM with `INotifyPropertyChanged`, commands, a navigation service, and page view models. UI state is separate from the savegame parser.
- **Dependency injection:** `Microsoft.Extensions.DependencyInjection` owns the foundation status, compatibility registry, navigation, theme, and main-window view models.
- **Theme:** one Fluent control theme and central theme dictionaries with dark and light palettes. The chosen palette is saved locally under the operating system's local application-data directory.
- **UI tests:** Avalonia headless plus xUnit v3 for a control-tree/window-load smoke test; regular unit tests cover view-model state, filtering, selected-page navigation, theme delegation, local theme storage, storage errors, and preview controls.

Avalonia's current desktop platform matrix includes Windows x64/ARM64, Linux x64/ARM64 across listed distributions, and macOS x64/ARM64. Linux uses X11 directly; Wayland is available through an opt-in backend package. These are framework capabilities, not verified build or runtime results for this application. See the [official supported-platform matrix](https://docs.avaloniaui.net/docs/supported-platforms), [DI/MVVM guide](https://docs.avaloniaui.net/docs/app-development/dependency-injection), and [headless xUnit guide](https://docs.avaloniaui.net/docs/testing/headless-xunit).

## Application composition

`Program` starts Avalonia's classic desktop lifetime. `App` registers the current empty `GameAdapterRegistry`, a status service, theme service, page catalog, navigation service and main-window view model in the DI container. The main window owns a searchable navigation rail, tracks the selected page, highlights it, and exposes each section's accessible name and help text. A content presenter displays the current page. The section view displays facts from the foundation service alongside explicit preview/empty states. At startup, the theme service reads the local light/dark preference; changes are written through a temporary file and same-directory replacement. If loading or saving fails, the app uses a safe default or the current-session selection and displays a status message.

The window can resize in the desktop layout; cards wrap inside a scrolling content area. The 7/30-day and Truck/Trailer/Coach selectors only update preview state. They do not query game data or claim those editors are implemented.

## Data and safety boundaries

- No game folder discovery, background watcher, file-picker, profile loader or save read is wired to the app.
- No save write, backup restore, quarantine, or process-control command exists in the UI.
- The only persisted desktop preference is the dark/light theme choice. It contains no account, game, or savegame data.
- The status cards use `GameAdapterRegistry.RegistrationCount`; the registry starts empty.
- Account values, vehicles, transactions, snapshots and diagnoses are not populated with invented examples.
- A future reader must be version-pinned and read-only first. It must pass real-fixture roundtrips and compatibility review before becoming discoverable from the UI.

## Build and run

From the repository root with the .NET 10 SDK:

```powershell
dotnet test TSSE.NextGen.sln --configuration Release
dotnet run --project desktop/TSSE.Desktop/TSSE.Desktop.csproj
```

The main CI publishes self-contained app outputs and starts/closes the native app on five automatic targets: Windows x64/ARM64, Linux x64/ARM64, and macOS Intel. macOS ARM64 has a separate manually triggered validation workflow and is not yet confirmed. Linux uses Xvfb for virtual display.

## Distribution notes

- **Windows:** Avalonia's Win32 backend; x64 and ARM64 publish and startup are CI-validated. MSI/WiX packaging and clean-machine installation/upgrade checks are still planned.
- **Linux:** Avalonia X11 backend. CI builds and installs DEB and RPM packages on Ubuntu 24.04 x64/ARM64, then launches and removes them. The RPM is installed on Ubuntu with dependency resolution disabled; Fedora-family dependency resolution/runtime and other distributions remain unverified. Wayland and AppImage are not tested.
- **macOS:** Avalonia's native Objective-C++ backend. Intel publish/startup is CI-validated. Apple Silicon packaging/runtime, code signing, hardened runtime, notarization and DMG installation remain unverified.

Self-contained .NET output removes the requirement for a separately installed .NET runtime, but it does not bundle every operating-system native library. Linux packages declare platform dependencies, but clean Fedora-family dependency resolution and installs on other distributions are still required before wider distribution.
