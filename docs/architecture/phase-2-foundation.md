# Phase 2 — Next Generation Rebuild

**Status: in progress.** Foundation and Avalonia UI are one combined Phase 2. There is no separate UI-migration Phase 3. Phase 2 remains open until the .NET 10 application starts successfully and the required build and test matrix has produced results.

## Current architecture

- C# / .NET 10 remains the application and savegame foundation. The existing `TSSE.Core`, `TSSE.Savegames`, `TSSE.GameAdapters`, `TSSE.Analytics`, and status-only `TSSE.Bridge` projects remain in the solution.
- `desktop/TSSE.Desktop` is the native desktop application, built with Avalonia 12.2.0, MVVM, Microsoft dependency injection, and a central Fluent theme with dark and light variants.
- The app contains navigation and interface previews for Command Center, Savegame Explorer, Vehicle Studio, Economy Intelligence, Savegame Time Machine, Compatibility Lab, Diagnostics Center, and Settings. Search filters the sections; vehicle category and analytics-window controls change preview state.
- Screens show only current foundation facts and explicit empty states. There are no sample balances, vehicles, profiles, snapshots, game diagnoses, or fabricated events.
- No profile scanning or real game-save reading is enabled. Save writes, restore actions, and background profile monitoring are disabled.
- Electron, Phoenix, Elixir, JavaScript UI assets, and TypeScript are not part of the new target architecture.

## Retained foundation and savegame safety

The SII syntax document and atomic file transaction remain intact. The parser preserves source text and supports a narrow update to an existing unique field; it does not implement complete SII semantics, compressed save containers, or game-version schemas. The transaction stages and validates caller-supplied bytes before replacement, checks the original SHA-256, retains a backup, and reports uncertain outcomes as `RecoveryRequired`.

`GameAdapterRegistry` starts empty. No game or save version is verified for reading or writing, so the UI cannot open or edit a production profile. The analytics library keeps recorded transactions distinct from snapshot-derived balance differences; it has no profile-backed history in the desktop app.

The parser has only synthetic fixtures. Genuine ETS2/ATS versions, mods, Coaches DLC, unknown-field roundtrips, and complete serializer behavior remain unverified. A successful generic atomic transaction is not evidence of a safe game-save edit.

## Avalonia application status

The Avalonia desktop project and headless UI/ViewModel tests have been added. It is designed to run natively on Windows, Linux, and macOS, with x64 and ARM64 targets. The six target RIDs are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`. Navigation now tracks and highlights the selected page; section buttons expose accessible names and help text.

Local validation uses .NET SDK 10.0.404/runtime 10.0.15 on Windows x64. The Release solution build succeeds with zero warnings and zero errors. All four MTP test executables pass (24/24 tests), including navigation selection, search/theme behavior, and a headless test that creates and shows the actual Avalonia `MainWindow`. A self-contained `win-x64` publish succeeds. The published Windows x64 application was launched as a native process; its window handle and title (`TS-SE Tool — Next Generation`) were observed. `CloseMainWindow()` sent `WM_CLOSE`, returned success, and the process exited. No stdout/stderr errors were emitted. Desktop automation could not capture or inspect the window because its access call returned `GetCursorPos: access denied`, so visual layout and user interaction remain unverified. Linux, macOS, and ARM64 native lifecycle remain untested locally.

## Phoenix / Elixir decision history

An earlier Phase 2 commit introduced a loopback Phoenix LiveView prototype, an OTP supervisor and a .NET status bridge. The prototype code had one-use launch-token, origin and session protections. Its Elixir tests were not run locally because Erlang/OTP and Elixir were unavailable; its JavaScript assets did build under Node 24. The corresponding GitHub runs failed before starting any jobs, so that architecture never received a CI result.

This prototype was a superseded experiment. Its implementation has now been removed after checking repository references. No Phoenix code was ported into Avalonia, and no Phoenix behavior is claimed as tested or delivered in the current application.

## GitHub Actions investigation

Runs [45638941710](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45638941710) and [45646310995](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45646310995) both recorded `failure` immediately after creation. GitHub's run API returned zero jobs; both runs had no check runs, logs, or artifacts. The connector does not expose the associated check-suite diagnostics or repository Actions settings, so the precise cause is undetermined. The earlier workflow's runner labels are listed by GitHub for public repositories, so there is no evidence here that those labels caused the zero-job result.

The .NET/Avalonia matrix is configured for six x64/ARM64 runner/RID combinations. Push run [45652466668](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45652466668) successfully created all six jobs, so the updated workflow did start. Every matrix job failed before tests or application compilation with the same `Microsoft.Testing.Platform.MSBuild 2.5.2` error: .NET 10 was invoking its default VSTest mode, which MTP v2 no longer supports. This explains that run's failures; it does not establish why the two earlier runs created zero jobs. The repository now selects MTP in `global.json`, uses xUnit's MTP v2 package `xunit.v3.mtp-v2` 3.2.2 (matching Avalonia.Headless.XUnit 12.2.0's xUnit API), removes the VSTest-only test SDK/adapter references, and uses .NET 10's `--solution` form. Follow-up run [45660844689](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45660844689) created all six target jobs; five completed successfully, including the full build/test and publish steps, while macOS ARM64 remained queued. Later documentation-only run [45663273345](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45663273345) on `b4f0feb` initially had four successes, a macOS Intel failure and a cancelled macOS ARM64 job. The macOS Intel retry succeeded on attempt 2, so that failure was not reproduced; GitHub's log endpoint returned `BlobNotFound`, leaving its cause unknown. Attempt 3 is rerunning macOS ARM64 and is still queued; the other five target combinations have succeeded. GitHub lists `macos-26` as an ARM64 label and notes macOS runners may take longer to start during high demand ([runner reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)). The navigation accessibility change described above is newer than these runs; it passes locally and awaits the next remote matrix.

## Validation history

- Earlier foundation commit: 17/17 synthetic .NET tests passed using a temporary `net8.0` target override with SDK 8.0.425. This was a compatibility smoke test, not a .NET 10 validation.
- Earlier Phoenix experiment: `npm ci` and asset bundling passed with Node 24 on Windows. Its Phoenix/OTP test suite did not run locally.
- Earlier .NET bridge: a manual local JSON-lines status request returned `foundation-only`, zero verified adapters, and writes disabled.
- Current Avalonia work: the Release solution builds on .NET SDK 10.0.404 with 0 warnings and 0 errors. The command used by CI, `dotnet test --solution TSSE.NextGen.sln --configuration Release`, passes locally with 24/24 tests: Core 7/7, Savegames 5/5, Integration 5/5, Desktop 7/7. The desktop tests cover ViewModel navigation selection/search/theme behavior and verify that the headless XAML window renders all eight navigation buttons with exactly one selected. Local file-transaction tests require `TEMP`/`TMP` to point to an ignored repository `bin` directory because the sandbox denies writes to the default system temp directory. A self-contained Windows x64 publish succeeds. A controlled native launch observed the window handle and title, and `CloseMainWindow()` successfully closed the process; desktop automation could not capture the window, so visual rendering and interaction were not independently inspected. CI run 45652466668 failed before compilation; after the MTP change, five targets passed in run 45660844689. Run 45663273345 (commit `b4f0feb`) has five successful targets after its macOS Intel retry; macOS ARM64 is still queued on attempt 3. The navigation accessibility changes in the current working tree have not yet run on GitHub Actions.

## Remaining work — still Phase 2

1. Finish the queued macOS ARM64 retry in run 45663273345; then verify all six hosted runner/RID combinations on the navigation-accessibility commit.
2. Verify native-window launch, visual rendering, interaction, and graceful close on Linux and macOS, including ARM64 where hardware or CI permits. Windows x64 launch and close have been verified; its visual rendering and interaction still need inspection.
3. Complete parser/container/version verification using representative safe fixtures before enabling read-only profile discovery.
4. Add full semantic roundtrip, save-version and crash-recovery tests before even considering game-specific writes. Keep writes disabled until these pass.
5. Finish accessible interaction, persistent local theme preferences, localization, and packaging/signing tests for Windows, Linux, and macOS.

There is no separate Phase 3 for these items. They remain in Phase 2 until the completion gate above is met.
