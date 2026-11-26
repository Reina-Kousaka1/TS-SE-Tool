# macOS packaging

The workflows are configured to create an unsigned `.app` bundle and compressed DMG from each macOS self-contained publish directory. The bundle contains the apphost and publish output in `Contents/MacOS`, an `Info.plist`, and the repository's Apache-2.0 license in `Contents/Resources`. The bundle identifier `com.github.reina-kousaka1.ts-se-tool` is derived from the repository identity and is provisional until signing/distribution setup is approved.

The macOS Intel job is configured to mount the DMG read-only, validate bundle metadata, start the apphost from the mounted image, and detach the image; this new workflow path still needs a successful CI run. The ARM64 workflow contains the same checks but has not completed because GitHub did not assign a runner. No package is signed, notarized, uploaded, or released. Gatekeeper, hardened runtime, Apple Silicon execution, icon/accessibility review, and DMG installation on a clean user system remain unverified.

Avalonia's [macOS deployment guide](https://docs.avaloniaui.net/docs/deployment/macos) documents the `.app` layout, `Info.plist`, and the additional signing/notarization work needed for distribution.
