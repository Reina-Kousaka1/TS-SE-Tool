# Native desktop hosts: not implemented yet

The current prototype is a loopback-only Phoenix LiveView runtime with an OTP-managed .NET console bridge. It is not a native desktop executable and does not claim to embed WebView2, WebKitGTK, or WKWebView.

Host feasibility and platform requirements are recorded in [the Phase 2 architecture note](../../docs/architecture/phase-2-foundation.md). Native host code, profile discovery, window lifecycle integration, and desktop end-to-end tests remain work for a later phase.
