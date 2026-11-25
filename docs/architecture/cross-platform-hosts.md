# Cross-platform host and distribution notes

The runtime is a local Phoenix application, not a remote service. A native desktop host must use the generated, per-run loopback address and one-use bootstrap credential; it must not bind a wildcard interface or enable remote origins.

## Native webview options

- **Windows — WebView2:** the supported Windows webview stack can host the LiveView page. Evergreen installation is smaller for the application but depends on the runtime being installed or bootstrapped; Fixed Version can be bundled for offline control but adds a large runtime payload and update responsibility. Neither option is integrated here.
- **Linux — WebKitGTK:** GTK/WebKitGTK offers an in-process native webview. Native package dependencies and ABI versions differ across distributions, so a single generic binary is not enough. The runtime is not tested against WebKitGTK.
- **macOS — WKWebView:** WKWebView integrates with AppKit, but hosting it from .NET requires tested Objective-C/native interop or a small native host. App signing and notarization still need a macOS packaging pipeline. No host is included.

All three can display Phoenix LiveView over local HTTP/WebSocket. Same-origin checks and LiveView session validation remain necessary; a loopback listener alone is not sufficient browser authentication.

## Runtime bundling

The .NET bridge should be published self-contained per RID, while an OTP release bundles the Erlang runtime, Elixir application and Hex dependencies. The desktop host should ship both payloads together and launch them with explicit executable paths rather than a shell command. The production session key must be generated and stored locally with owner-only access; it must never be committed or sent to a service.

Future packaging targets: MSI/WiX for Windows, DEB and RPM for Linux, and a signable `.app` plus DMG for macOS. A packaged runtime must be tested on clean machines without development SDKs. This phase creates no package artifacts and does not validate installation or upgrade behavior.
