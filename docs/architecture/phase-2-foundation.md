# Phase 2 architecture foundation

## Scope delivered

- A new .NET 10 solution isolates `TSSE.Core`, `TSSE.Savegames`, `TSSE.GameAdapters`, `TSSE.Analytics`, and `TSSE.Bridge` from the untouched WinForms application.
- The syntax foundation keeps original SII text, line endings, unknown blocks, raw values, arrays, `nil`, and integer text. It supports a narrow update to one existing uniquely identified field. It does not interpret game semantics, decode compressed/encrypted save containers, validate a game schema, or recognize any ETS2/ATS save version.
- The atomic file transaction stages and flushes bounded bytes, validates the complete candidate before replacement, checks the caller's original SHA-256 to detect stale input, retains an original backup, and reports uncertain outcomes as `RecoveryRequired`. It is not connected to game profiles. It does not preserve every ACL/metadata combination, promise power-loss durability on every filesystem, protect against a hostile process racing a trusted parent directory, or provide an automated recovery UI.
- Game adapter registry starts empty. Unknown game/version pairs are unsupported and not writable. The bridge allows only `engine.status`; it reports `foundation-only`, zero verified adapters, and writes disabled.
- Analytics distinguishes user-provided transaction facts from balance/net-worth differences between local snapshots. It does not derive historical transactions from balance changes.
- Phoenix LiveView is served only on `127.0.0.1`; HTTP Host/Origin and WebSocket Origin are checked. The launch credential is a cryptographically random one-use token delivered only through a URL fragment, then exchanged in a CSRF-protected same-origin POST. The session cookie is HttpOnly, encrypted/signed by Plug.Session, SameSite Strict, and short-lived. LiveView checks the authenticated session on mount. There is no CORS configuration.
- The OTP supervisor manages an actual `.NET` bridge process through a bounded JSON-lines port client and closes the port when the client shuts down. If no bridge executable is configured in development, the UI says the bridge is unavailable; it does not synthesize a success response.

## SII and game compatibility status

The parser is a line-preserving syntax scaffold tested against a synthetic fixture. Its supported grammar is deliberately narrower than the real SII ecosystem; inline-comment rules, all quoting/escape rules, binary/compressed save wrappers, semantic types, game schema versions, mods, and Coaches DLC have not been confirmed against genuine saves. There are no real ETS2 or ATS fixtures in this phase. Consequently the project registers no read or write adapters and does not enable save writes. A syntax roundtrip test is not evidence of compatibility with a game save.

The requested references were reviewed: [PR #147](https://github.com/LIPtoH/TS-SE-Tool/pull/147) documents a v97 write path that could truncate a save before serialization completed, and reports raw-value edge cases and dropped fields; [PR #149](https://github.com/LIPtoH/TS-SE-Tool/pull/149) contains broad ETS2 1.61 compatibility edits and asks for additional ATS testing. No code from either PR was copied into the new engine. This foundation addresses only the general staged-write failure mode and text-preserving syntax groundwork; neither PR establishes version compatibility for this implementation.

The transaction primitive has only synthetic temporary-file tests. Its validator is a required caller-supplied function; no game-specific serializer/validator is currently able to approve a save. Even a successful generic file transaction must not be represented as a verified in-game save edit.

## UI host feasibility

| Platform | Suitable host | Current assessment | Implemented / tested |
| --- | --- | --- | --- |
| Windows x64 / ARM64 | WebView2 | Feasible through a native .NET host. Distribution must account for Evergreen Runtime availability or bundle a Fixed Version runtime; this repository has no host project yet. | No host code or Windows WebView test. |
| Linux x64 / ARM64 | WebKitGTK | Feasible through a native GTK/WebKitGTK host or a small native bridge. GTK/WebKit ABI and package dependencies vary by distribution and must be packaged per target. | No host code, GTK bindings, or WebKitGTK test. |
| macOS Intel / Apple Silicon | WKWebView | Feasible through AppKit/Objective-C interop. Signing, hardened runtime, and notarization are distribution requirements. | No host code, native framework test, signing, or notarization. |

Phoenix/LiveView serves same-origin HTML and WebSocket traffic, so the UI protocol can be embedded in platform WebViews. That does not by itself create a desktop application: the native host still must launch and supervise OTP, capture the private startup handshake, open the exact loopback URL, handle window close, and shut down the runtime. The current handshake is a prototype contract, not a tested host integration.

The `.NET` CI matrix includes Windows ARM64. The current setup-beam action advertises OTP support for Windows x64, but not Windows ARM64; therefore the real .NET/OTP process integration runs on Linux x64/ARM64, Windows x64, and macOS x64/ARM64. Windows ARM64 Phoenix/OTP remains unconfirmed.

## Packaging plan, not delivered

- Publish the .NET bridge self-contained for each target RID and include it in an OTP release. OTP releases bundle the BEAM runtime and application dependencies; each OS/architecture still needs its own built release.
- Windows: native WebView2 host and MSI/WiX packaging; choose Evergreen bootstrap versus Fixed Version payload and test offline installation.
- Linux: GTK/WebKitGTK native host; produce DEB and RPM packages, and evaluate AppImage only after system WebKitGTK dependency behavior is proven.
- macOS: WKWebView app bundle and DMG, then sign and notarize per architecture.
- The current CI builds the .NET bridge self-contained and runs runtime integration tests. It produces no installer, app bundle, release, or deployment.

## Next phase

1. Obtain representative, explicitly synthetic or donated genuine save samples for supported game versions and inspect PR #147/#149 against their actual changes before reuse.
2. Specify and test the complete SII/container grammar, typed game models, schema/version detection, and a lossless serializer. Keep all writes disabled until semantic validation is tied to an exact verified game version.
3. Add recovery journal semantics and fault-injection coverage for filesystem failures and process interruption; assess filesystem guarantees on Windows, Linux, and macOS.
4. Build one native desktop host proof of concept, starting with Windows WebView2, and verify private runtime launch, authenticated profile read-only flow, and clean shutdown. Repeat with WebKitGTK and WKWebView before claiming cross-platform support.
5. Add per-target packaging and offline-install tests. Do not use real profile writes until specific compatibility and recovery checks pass.

## Validation snapshot (2026-11-25)

- The synthetic .NET test suite passed 17/17 tests using a temporary `net8.0` target override with SDK 8.0.425. This is a compatibility smoke test only; the committed projects target .NET 10 and have not yet been built with a .NET 10 SDK.
- The local JavaScript asset install and bundle passed with Node.js 24 on Windows.
- Erlang/OTP, Elixir, and `actionlint` were not installed locally. The Phoenix tests and OTP-to-.NET process integration were therefore not run locally. The .NET bridge was separately started and answered a local JSON-lines status request.
- GitHub Actions run `45638941710` for the initial implementation commit completed with failure before any jobs were created. The API reported zero jobs, zero check runs, and no logs. The cause is undetermined, so no GitHub-hosted platform matrix result is claimed.
- No native WebView host, real ETS2/ATS save compatibility, game-specific serialization, real-profile write, ARM64 runtime integration on Windows, or installer has been tested.
