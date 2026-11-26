# macOS packaging

The workflows are configured to create an unsigned `.app` bundle and compressed DMG from each macOS self-contained publish directory. The bundle contains the apphost and publish output in `Contents/MacOS`, an `Info.plist`, and the repository's Apache-2.0 license in `Contents/Resources`. The bundle identifier `com.github.reina-kousaka1.ts-se-tool` is derived from the repository identity and is provisional until signing/distribution setup is approved.

Runs [45729354582](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45729354582) and [45729354678](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45729354678) passed the Intel and Apple Silicon `.app`/DMG builds, plist validation, read-only mounts, bundled-app launches, and detach checks. No package is signed, notarized, uploaded, or released. Gatekeeper, hardened runtime, signing, notarization, icon/accessibility review, and DMG installation on a clean user system remain unverified.

Avalonia's [macOS deployment guide](https://docs.avaloniaui.net/docs/deployment/macos) documents the `.app` layout, `Info.plist`, and the additional signing/notarization work needed for distribution.
