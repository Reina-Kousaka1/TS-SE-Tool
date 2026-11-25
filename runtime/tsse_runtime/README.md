# Local Phoenix / OTP prototype

This app serves a Phoenix LiveView status page on IPv4 loopback and starts the configured .NET bridge as a supervised Erlang port. It does not expose profile listing or save editing.

## Development prerequisites

- .NET 10 SDK
- Erlang/OTP 29.1 and Elixir 1.20.5
- Node.js 24 for asset bundling only; the generated runtime does not require Node.js

From this directory, restore JavaScript and Hex dependencies, build the local LiveView client, and publish the .NET bridge as a local self-contained executable. Then set `TSSE_BRIDGE_EXECUTABLE` to that executable and start `mix phx.server`. For an external launcher prototype, set `TSSE_LAUNCH_HANDSHAKE=1`; the runtime writes one JSON line containing a URL with a one-use token in its fragment. Keep that process output private because it carries the launch credential.

The production runtime also requires a locally provisioned `TSSE_SECRET_KEY_BASE` (at least 64 bytes) and a packaged bridge executable. It refuses to start in production when either is absent. A future native launcher must provision the key locally, capture the handshake through a private pipe, open the supplied URL in its embedded host, and stop the supervised runtime on window close. No such launcher exists yet.

The local test suite includes token and origin checks. CI builds and starts the actual self-contained .NET bridge on supported x64/ARM64 runner images and checks its JSON status over the OTP port. No in-game savefile is opened or modified.
