# macOS packaging

The workflows are configured to create an unsigned `.app` bundle and compressed DMG from each macOS self-contained publish directory. The bundle contains the apphost and publish output in `Contents/MacOS`, an `Info.plist`, and the repository's Apache-2.0 license in `Contents/Resources`. The bundle identifier `com.github.reina-kousaka1.ts-se-tool` is derived from the repository identity and is provisional until signing/distribution setup is approved.

Run [45728314958](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45728314958) passed the macOS Intel `.app`/DMG build, plist validation, read-only mount, bundled-app launch, and detach checks. The ARM64 workflow contains the same checks but has not completed because GitHub did not assign a runner. No package is signed, notarized, uploaded, or released. Gatekeeper, hardened runtime, Apple Silicon execution, icon/accessibility review, and DMG installation on a clean user system remain unverified.

Avalonia's [macOS deployment guide](https://docs.avaloniaui.net/docs/deployment/macos) documents the `.app` layout, `Info.plist`, and the additional signing/notarization work needed for distribution.
