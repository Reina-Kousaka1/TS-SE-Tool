# Avalonia desktop architecture

## Technology choices

- **UI and desktop host:** Avalonia 12.2.0 on .NET 10. The same native UI project targets Windows, Linux, and macOS without a browser runtime.
- **Presentation pattern:** MVVM with `INotifyPropertyChanged`, commands, a navigation service, and page view models. UI state is separate from the savegame parser.
- **Dependency injection:** `Microsoft.Extensions.DependencyInjection` owns the foundation status, compatibility registry, navigation, theme, and main-window view models.
- **Theme:** one Fluent control theme and central theme dictionaries with dark and light palettes. The chosen palette is saved locally under the operating system's local application-data directory.
- **UI tests:** Avalonia headless plus xUnit v3 checks the loaded control tree and sends mouse and text input through the actual XAML controls. Coverage includes all eight navigation items, search filtering, the theme switch, vehicle categories, and analytics-window selection. Regular unit tests also cover view-model state, theme delegation, local theme storage, storage errors, and preview controls.

The CI configuration captures dark-theme screenshots of all eight pages and a light-theme Coaches preview from synthetic, empty foundation state. The Linux x64 job uploads them as a one-day workflow artifact; screenshots are written under the runner's temporary directory and contain no user profiles or savegame data. Artifact [45742296246](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45742296246) was reviewed at 1360×900, including the wrapped active navigation title. The upload step passed again in run [45753323007](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45753323007). This is headless visual evidence only; physical window rendering, accessibility, keyboard and pointer behavior, and localized layouts still require review on desktop systems.

Avalonia's framework platform matrix includes Windows x64/ARM64, Linux x64/ARM64 across listed distributions, and macOS x64/ARM64. This app's latest successful CI validates Windows x64/ARM64, Linux x64/ARM64, and macOS Intel x64. Apple Silicon passed an earlier run, [45793859871](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45793859871) on `e5d97fa`; the later bounded-reader line-limit change has not passed on that target because [45795444731](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45795444731) was cancelled before a runner or any job steps were recorded. Linux validation uses X11/Xvfb; Wayland is available through an opt-in backend package but is not tested here. See the [official supported-platform matrix](https://docs.avaloniaui.net/docs/supported-platforms), [DI/MVVM guide](https://docs.avaloniaui.net/docs/app-development/dependency-injection), and [headless xUnit guide](https://docs.avaloniaui.net/docs/testing/headless-xunit).

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

The main CI publishes self-contained app outputs and starts/closes the native app on five automatic targets: Windows x64/ARM64, Linux x64/ARM64, and macOS Intel. A separate explicit macOS ARM64 workflow targets GitHub's `macos-15` M1 runner. It passed its full build, tests, publish, app bundle/DMG, and startup checks on older code in [45793859871](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45793859871); the latest bounded-reader change remains unverified there after [45795444731](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45795444731) cancelled without a runner or test steps. Linux uses Xvfb for virtual display.

## Distribution notes

- **Windows:** Avalonia's Win32 backend; x64 and ARM64 publish and startup are CI-validated. MSI/WiX packaging and clean-machine installation/upgrade checks are still planned.
- **Linux:** Avalonia X11 backend. CI builds and installs DEB and RPM packages on Ubuntu 24.04 x64/ARM64, then launches and removes them. Fedora 45 x86_64/aarch64 CI also builds the RPM, resolves dependencies through DNF, launches the installed app under Xvfb, and removes it. Fedora signatures, Wayland, Fedora workstation installs, and other distributions remain unverified; AppImage is not tested.
- **macOS:** Avalonia's native Objective-C++ backend. CI currently validates the unsigned Intel `.app` bundle and DMG, including a read-only mount and bundled-app launch. The Apple Silicon workflow contains the same packaging checks and passed them on older source in run [45793859871](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45793859871); the latest parser change has no ARM64 validation. Code signing, hardened runtime, notarization and clean-user DMG installation remain unverified.

Self-contained .NET output removes the requirement for a separately installed .NET runtime, but it does not bundle every operating-system native library. Linux packages declare platform dependencies; Fedora 45 dependency resolution and Xvfb startup are CI-validated, while clean workstation installs and other distributions still require verification before wider distribution.
