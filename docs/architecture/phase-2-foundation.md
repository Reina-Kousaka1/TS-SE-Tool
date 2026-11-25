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

The Avalonia desktop project and headless UI/ViewModel tests have been added. It is designed to run natively on Windows, Linux, and macOS, with x64 and ARM64 targets. The six target RIDs are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`.

The local machine currently has .NET SDK 8.0.425, not .NET 10. The project targets `net10.0`; therefore a native .NET 10 build or visible desktop launch has not yet been demonstrated locally. The headless tests and .NET 10 build are configured in GitHub Actions, but the prior Actions runs stopped before creating jobs. Avalonia supports the desktop targets described in its [platform matrix](https://docs.avaloniaui.net/docs/supported-platforms); support at the framework level does not establish that this app has passed on those targets.

## Phoenix / Elixir decision history

An earlier Phase 2 commit introduced a loopback Phoenix LiveView prototype, an OTP supervisor and a .NET status bridge. The prototype code had one-use launch-token, origin and session protections. Its Elixir tests were not run locally because Erlang/OTP and Elixir were unavailable; its JavaScript assets did build under Node 24. The corresponding GitHub runs failed before starting any jobs, so that architecture never received a CI result.

This prototype was a superseded experiment. Its implementation has now been removed after checking repository references. No Phoenix code was ported into Avalonia, and no Phoenix behavior is claimed as tested or delivered in the current application.

## GitHub Actions investigation

Runs [45638941710](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45638941710) and [45646310995](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45646310995) both recorded `failure` immediately after creation. GitHub's run API returned zero jobs; both runs had no check runs, logs, or artifacts. The connector does not expose the associated check-suite diagnostics or repository Actions settings, so the precise cause is undetermined. The earlier workflow's runner labels are listed by GitHub for public repositories, so there is no evidence here that those labels caused the zero-job result.

The workflow has been replaced with one .NET/Avalonia matrix that builds and tests the solution and publishes the desktop app for each of the six RIDs. The push of commit `e355b732e840a72d2975cb932fe834505118006d` did not create a run in the Actions API. A manual `workflow_dispatch` against this fork returned HTTP 500 and created no run. Thus the new workflow has not yet executed, and the cause of the earlier zero-job failures or the missing run remains unconfirmed; repository-side Actions configuration and platform-side dispatch handling have not been isolated.

## Validation history

- Earlier foundation commit: 17/17 synthetic .NET tests passed using a temporary `net8.0` target override with SDK 8.0.425. This was a compatibility smoke test, not a .NET 10 validation.
- Earlier Phoenix experiment: `npm ci` and asset bundling passed with Node 24 on Windows. Its Phoenix/OTP test suite did not run locally.
- Earlier .NET bridge: a manual local JSON-lines status request returned `foundation-only`, zero verified adapters, and writes disabled.
- Current Avalonia work: the test project contains view-model/navigation tests and an Avalonia headless window-load test. The installed SDK cannot build the `net10.0` solution (`NETSDK1045`). A temporary, automatically restored `net8.0` compatibility run passed the 17 existing Core/Savegames/Integration tests, but Avalonia compilation stopped with `CS9057`: its 12.2.0 source generators require Roslyn 4.14 while SDK 8 supplies Roslyn 4.11. Disabling analyzers did not avoid loading those generators; the temporary Roslyn 4.14 workaround could not run because its build task requires .NET runtime 9, which is absent. All temporary project edits were restored. The Avalonia project has not compiled, its new UI tests have not run, and the application has not been demonstrated to start.

## Remaining work — still Phase 2

1. Obtain a .NET 10 SDK/runtime environment, compile the Avalonia project, run all tests including the headless UI tests, and publish on every available OS/architecture target; investigate repository-side Actions configuration if the new workflow again starts no jobs.
2. Demonstrate the visible application launch on Windows, Linux, and macOS, including x64 and ARM64 where hardware or CI permits.
3. Complete parser/container/version verification using representative safe fixtures before enabling read-only profile discovery.
4. Add full semantic roundtrip, save-version and crash-recovery tests before even considering game-specific writes. Keep writes disabled until these pass.
5. Finish accessible interaction, persistent local theme preferences, localization, and packaging/signing tests for Windows, Linux, and macOS.

There is no separate Phase 3 for these items. They remain in Phase 2 until the completion gate above is met.
