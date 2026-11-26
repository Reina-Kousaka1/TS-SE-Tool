# Local in-game snapshot analytics

## Scope

This foundation adds a profile-scoped analytics path over locally stored balance snapshots. It calculates cash, bank-balance, and net-worth changes from snapshots whose game time is supplied as a normalized `TimeSpan`. A snapshot delta is a calculated balance difference; it is not a recorded transaction.

The current repository has no verified ETS2 or ATS adapter that can supply this game time. The analytics API therefore accepts the value but does not decode or infer it from SII text. The desktop UI remains a preview until a version-pinned, read-only adapter can provide verified data.

## Time and identity rules

- The caller supplies the exact game ID, stable profile ID, end game time, and window length.
- The requested window is selected by game time, never by the workstation clock. `CapturedAt` is used only to order observations and detect a game-time rollback.
- A 7-day or 30-day total is available only when observations cover both requested game-time boundaries. Incomplete history is reported as insufficient coverage and does not expose a partial delta as a full-window result.
- Calculations use snapshots from exactly one game and profile, and one save version. Missing game time within the observed capture interval, a detected rollback, a version boundary, duplicate game times, or duplicate snapshot IDs withholds the affected deltas.
- A daily delta requires observations at both normalized in-game-day boundaries. Empty or partially sampled days remain null; no transaction categories or historical events are synthesized.
- `InGameTime` is optional in local snapshot JSON. Older entries remain readable with no migration and are not eligible for in-game calculations.

## Provenance limits

The analytics layer does not authenticate snapshot contents or detect manual edits to the local JSON history or to game balances. Such changes cannot be distinguished from ordinary balance changes without a verified source of record or tamper-evident provenance. Reports therefore describe only observed snapshot differences; they must not label them as transactions or confirmed game events. A clock rollback is detected only when it is visible in the captured in-game-time sequence. A restore or edit that leaves no such evidence is not detectable here.

## Architecture self-review

No Architect tool is available. This is the implementation agent's documented self-review, not an independent review. Before implementation, the design was checked against the existing snapshot store, profile-isolation tests, bounded parser, and data-integrity requirements. It preserves the existing real-clock analytics API, adds an explicitly separate game-time API, and keeps the storage format backward-readable by adding one optional field. A full-window result now requires coverage of both requested game-time boundaries. Missing time, clock rollback, save-version boundary, duplicate times/IDs, malformed entries, or incomplete coverage produce a status and null deltas rather than a guessed full-window result. Regression coverage for 7-/30-day windows, profile isolation, reset, duplicates, missing data, save versions, daily boundary coverage, and JSON compatibility passed in GitHub Actions run [45896687312](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45896687312): 114/114 tests on each of five desktop targets and 105/105 on each Fedora x64/ARM64 job.

The main unresolved risk is upstream time provenance: without a verified game adapter, no production data may be labeled with this normalized time. The API does not establish save-version compatibility, decode compressed saves, prove transaction history, or enable profile discovery or writes. The current commit was not run on macOS ARM64; the separate Apple Silicon validation applies only to earlier source. Tests validate the analytics logic and JSON persistence behavior with synthetic records, not actual ETS2/ATS saves.
