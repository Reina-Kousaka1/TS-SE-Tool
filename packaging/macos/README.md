# macOS packaging

The workflows are configured to create an unsigned `.app` bundle and compressed DMG from each macOS self-contained publish directory. The bundle contains the apphost and publish output in `Contents/MacOS`, an `Info.plist`, and the repository's Apache-2.0 license in `Contents/Resources`. The bundle identifier `com.github.reina-kousaka1.ts-se-tool` is derived from the repository identity and is provisional until signing/distribution setup is approved.

Full matrix [45787021869](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45787021869) passed the Intel `.app`/DMG build, plist validation, read-only mount, bundled-app launch, and detach check, plus 86/86 tests on macOS Intel. Explicit Apple Silicon run [45787021904](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45787021904) passed 86/86 .NET 10 tests, self-contained publish, app bundle/DMG validation, and native startup/shutdown. No package is signed, notarized, uploaded, or released. Gatekeeper, hardened runtime, signing, notarization, icon/accessibility review, and DMG installation on a clean user system remain unverified.

Avalonia's [macOS deployment guide](https://docs.avaloniaui.net/docs/deployment/macos) documents the `.app` layout, `Info.plist`, and the additional signing/notarization work needed for distribution.
