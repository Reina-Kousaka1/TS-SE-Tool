# Phase 2 — Next Generation Rebuild

**Status: in progress.** Foundation and Avalonia UI are one combined Phase 2. There is no separate UI-migration Phase 3. Run [45688114218](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45688114218) passed the .NET 10 build, full tests and self-contained publish on five targets; native startup and shutdown passed on Linux x64/ARM64, Windows x64/ARM64 and macOS Intel. The macOS ARM64 job and its targeted rerun were cancelled after 15 minutes before any step ran. The app's Windows x64 window title was observed locally, but visual layout and interaction remain unverified. Phase 2 stays open pending macOS ARM64 validation, UI interaction review, real game-save compatibility evidence, and packaging/signing checks.

## Current architecture

- C# / .NET 10 remains the application and savegame foundation. The existing `TSSE.Core`, `TSSE.Savegames`, `TSSE.GameAdapters`, `TSSE.Analytics`, and status-only `TSSE.Bridge` projects remain in the solution.
- `desktop/TSSE.Desktop` is the native desktop application, built with Avalonia 12.2.0, MVVM, Microsoft dependency injection, and a central Fluent theme with dark and light variants.
- The app contains navigation and interface previews for Command Center, Savegame Explorer, Vehicle Studio, Economy Intelligence, Savegame Time Machine, Compatibility Lab, Diagnostics Center, and Settings. Search filters the sections; vehicle category and analytics-window controls change preview state.
- Dark/light theme selection is now remembered in a small local preference file. The window reports when the preference could not be read or saved; this setting stores no game or savegame data.
- Screens show only current foundation facts and explicit empty states. There are no sample balances, vehicles, profiles, snapshots, game diagnoses, or fabricated events.
- No profile scanning or real game-save reading is enabled. Save writes, restore actions, and background profile monitoring are disabled.
- Electron, Phoenix, Elixir, JavaScript UI assets, and TypeScript are not part of the new target architecture.

## Retained foundation and savegame safety

The SII syntax document and atomic file transaction remain intact. The parser preserves source text and supports a narrow update to an existing unique field; it does not implement complete SII semantics, compressed save containers, or game-version schemas. The transaction stages and validates caller-supplied bytes before replacement, checks the original SHA-256, retains a backup, and reports uncertain outcomes as `RecoveryRequired`.

`GameAdapterRegistry` starts empty. No game or save version is verified for reading or writing, so the UI cannot open or edit a production profile. The analytics library keeps recorded transactions distinct from snapshot-derived balance differences; it has no profile-backed history in the desktop app.

The parser has only synthetic fixtures. Genuine ETS2/ATS versions, mods, Coaches DLC, unknown-field roundtrips, and complete serializer behavior remain unverified. A successful generic atomic transaction is not evidence of a safe game-save edit.

## Avalonia application status

The Avalonia desktop project and headless UI/ViewModel tests have been added. It is designed to run natively on Windows, Linux, and macOS, with x64 and ARM64 targets. The six target RIDs are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`. Navigation now tracks and highlights the selected page; section buttons expose accessible names and help text.

The previous Windows validation used .NET SDK 10.0.404/runtime 10.0.15: the Release solution build passed with zero warnings/errors, all 24 then-existing tests passed, a self-contained `win-x64` publish succeeded, and the published application started and closed as a native process. Its window handle and title (`TS-SE Tool — Next Generation`) were observed. Desktop automation could not inspect it because its access call returned `GetCursorPos: access denied`; visual layout and interaction remain unverified. The current environment exposes only .NET SDK 8.0.425, so new changes use CI for .NET 10 validation. Run 45688114218 has passed build, tests, publish, and native startup/shutdown on five targets. Both macOS ARM64 attempts were cancelled after 15 minutes without any step starting; each GitHub job record has `runner_id=0`, empty runner and runner-group names, and no steps, while log retrieval returned `BlobNotFound`. This confirms that GitHub did not assign a runner, but does not reveal why. The `macos-26` label is a documented ARM64 runner label; availability versus repository/organization policy remains unresolved. A further retry is pending an architect review because the same runner-allocation failure repeated.

## Phoenix / Elixir decision history

An earlier Phase 2 commit introduced a loopback Phoenix LiveView prototype, an OTP supervisor and a .NET status bridge. The prototype code had one-use launch-token, origin and session protections. Its Elixir tests were not run locally because Erlang/OTP and Elixir were unavailable; its JavaScript assets did build under Node 24. The corresponding GitHub runs failed before starting any jobs, so that architecture never received a CI result.

This prototype was a superseded experiment. Its implementation has now been removed after checking repository references. No Phoenix code was ported into Avalonia, and no Phoenix behavior is claimed as tested or delivered in the current application.

## GitHub Actions investigation

Runs [45638941710](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45638941710) and [45646310995](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45646310995) were both `push` events on the correct development branch and referenced `.github/workflows/next-gen-ci.yml`. Each run failed in the same second it was created. The Actions jobs endpoint and commit check-runs endpoint both returned zero entries; neither run has logs or artifacts. Their GitHub Actions check suites failed immediately, but there are no check-run diagnostics identifying the cause. The exact initialization cause remains unproven. A later run created all matrix jobs, so the workflow did run on subsequent commits.

The .NET/Avalonia matrix is configured for six x64/ARM64 runner/RID combinations. Push run [45652466668](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45652466668) successfully created all six jobs, so the updated workflow did start. Every matrix job failed before tests or application compilation with the same `Microsoft.Testing.Platform.MSBuild 2.5.2` error: .NET 10 was invoking its default VSTest mode, which MTP v2 no longer supports. This explains that run's failures; it does not establish why the two earlier runs created zero jobs. The repository now selects MTP in `global.json`, uses xUnit's MTP v2 package `xunit.v3.mtp-v2` 3.2.2 (matching Avalonia.Headless.XUnit 12.2.0's xUnit API), removes the VSTest-only test SDK/adapter references, and uses .NET 10's `--solution` form. Follow-up run [45660844689](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45660844689) created all six target jobs; five completed successfully, including the full build/test and publish steps, while macOS ARM64 remained queued. Later run [45663273345](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45663273345) on `b4f0feb` initially had four successes, a macOS Intel failure and a cancelled macOS ARM64 job. The macOS Intel retry succeeded on attempt 2, so that failure was not reproduced; GitHub's log endpoint returned `BlobNotFound`, leaving its cause unknown. The macOS ARM64 attempt was also delayed. GitHub lists `macos-26` as an ARM64 label and notes macOS runners may take longer to start during high demand ([runner reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)).

The navigation accessibility change was validated by push run [45680427306](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45680427306) on commit `d6a98b6`: all six matrix jobs completed successfully, each running the full build/test and desktop publish steps. The subsequent documentation-only runs [45681937591](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45681937591) and [45683413861](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45683413861) each had five successful targets and one cancelled macOS ARM64 job; neither had a failed build/test step.

## Validation history

- Earlier foundation commit: 17/17 synthetic .NET tests passed using a temporary `net8.0` target override with SDK 8.0.425. This was a compatibility smoke test, not a .NET 10 validation.
- Earlier Phoenix experiment: `npm ci` and asset bundling passed with Node 24 on Windows. Its Phoenix/OTP test suite did not run locally.
- Earlier .NET bridge: a manual local JSON-lines status request returned `foundation-only`, zero verified adapters, and writes disabled.
- Earlier local Avalonia validation: .NET SDK 10.0.404/runtime 10.0.15 built the Release solution with 0 warnings/errors and passed 24 tests (Core 7, Savegames 5, Integration 5, Desktop 7). A self-contained Windows x64 publish and native window open/close succeeded. Visual inspection was unavailable because desktop automation returned `GetCursorPos: access denied`.
- Current environment: only .NET SDK 8.0.425 is installed, so the local theme-preference and CI smoke-test changes were validated through GitHub Actions, not locally. Run [45688114218](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45688114218), attempt 1 on `8ae805b`, passed build, tests, publish, and native open/close on Linux x64/ARM64, Windows x64/ARM64, and macOS Intel. The Linux x64 log reports 29/29 tests: Core 7, Savegames 5, Integration 5, Desktop 12. Its macOS ARM64 job was cancelled after 15 minutes without running a step; a targeted retry (attempt 2) was also cancelled after 15 minutes with zero steps. Both log endpoints returned `BlobNotFound`. The repeated cancellation is consistent with runner provisioning/availability, but GitHub exposes no diagnostic that proves the cause. All other five target jobs passed, including their native smoke steps.

## Remaining work — still Phase 2

1. Obtain the required architect review of the repeated macOS ARM64 runner cancellation, then complete its native startup/shutdown smoke test.
2. Inspect visual rendering and user interaction on the supported desktop environments. Native launch/close is verified on Windows x64/ARM64, Linux x64/ARM64, and macOS Intel; macOS ARM64 remains unverified.
3. Complete parser/container/version verification using representative safe fixtures before enabling read-only profile discovery.
4. Add full semantic roundtrip, save-version and crash-recovery tests before even considering game-specific writes. Keep writes disabled until these pass.
5. Finish accessible interaction, localization, and packaging/signing tests for Windows, Linux, and macOS.

There is no separate Phase 3 for these items. They remain in Phase 2 until the completion gate above is met.
