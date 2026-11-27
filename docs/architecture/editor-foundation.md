# Editor foundation

The editor session in TSSE.Core is an in-memory foundation. It cannot discover,
open, serialize, or write a game save. ProfileValues holds only balance and
experience as detached values; it is not a complete profile or a versioned SII
mapping. A new session has an original baseline. Valid edits create snapshots,
undo and redo move through them, and a different edit after undo drops redo.
Unchanged or invalid edits leave history intact. Callers of the generic history
must supply immutable snapshots and a validator.

The Savegame Explorer offers a clearly labelled temporary profile demo for
balance and experience, with per-field validation and undo/redo. Its initial
zeroes are scratch values, not data read from a profile. The demo edits and
displays an embedded synthetic SII document in memory. No loading or saving
is connected to this demo. Vehicle and job editing and real profile mapping
remain future work.

CI evidence for core commit 29add2b: run 45980964128 passed its .NET 10 test
steps on all seven jobs. Six jobs completed successfully; the Linux x64 job
failed later while refreshing the Microsoft Ubuntu package feed (HTTP 403),
before its package smoke tests. This is an external feed failure, not evidence
that packaging passed.

The legacy application's Bank.cs represents money_account as Int64 and
Economy.cs represents experience_points as UInt32. This informs the isolated
value ranges only; it does not prove compatibility with current game versions.
Negative balances are accepted; experience is unsigned. Input parsing is
invariant decimal without whitespace or thousands separators. Level conversion
and game-specific rules are intentionally unavailable pending compatibility
evidence. No file or SII field is changed by the session.

## In-memory SII edit batches

`SiiEditSession` stages explicit field replacements against an already parsed
syntax document and groups each batch into one undo entry. A failed replacement,
duplicate target, or unclassified syntax leaves the current text and redo history
unchanged. Unchanged replacements do not create history entries. This is a
syntax-level facility for synthetic tests; it has no file access, save-version
validation, or mapping from profile models to real game files. The UI profile
demo uses only embedded synthetic text. Unknown recognized fields and blocks remain in the
original text; unclassified syntax blocks edits.

Architecture self-review (not independent): existing `WithFieldValue` reparses
each candidate and checks block layout and exact selected value. The new session
only publishes a candidate after every replacement succeeds. It does not claim
that syntactic success means game validity. Full-text snapshots and reparsing
can be expensive; bounded history and game-specific validation are required
before real profile editing.

## Experimental profile mapping

`LegacyProfilePreview` resolves an explicitly selected economy block's bank
reference and maps legacy `money_account` (Int64) and `experience_points`
(UInt32) fields for in-memory synthetic fixtures. These names and types are
supported by the legacy Bank and Economy implementations, not validated
against current games. Missing or duplicate targets, invalid numbers and
unclassified syntax fail closed. Only changed values are replaced, in one batch;
unchanged number formatting and unknown fields remain intact. The UI uses this
mapping for its embedded example; file operations do not use it. Actual game-version validation, a verified
adapter, and explicit write authorization remain mandatory.

Architecture self-review (not independent): the reference is resolved once
from the selected economy block, and this mapping has no operation to change
that reference. The underlying syntax session makes the batch atomic; no save
operation is provided. Explicit selection is not evidence that the economy
block belongs to a real user profile, so this interface is synthetic-only.

UI self-review (not independent): the view model constructs only a constant
synthetic document, has no file selection or save command, and exposes the
updated text as a visibly labelled preview. The adapter and syntax session stay
in memory; no feature gate is changed. Test evidence must cover validation,
undo/redo and preservation of an unknown field in the displayed preview.

## Architecture self-review (not independent)

Decision: keep the editor core detached from file transactions and SII until
game-specific validation is established. History relies on immutable values;
using a mutable object would invalidate undo guarantees. ProfileValues is an
immutable record. Unlimited history is acceptable for this isolated foundation,
but a bounded strategy is needed before arbitrary large documents are connected.
Compatibility, persistence, model-to-SII mapping and independent recovery
review remain open; no write feature gate has been changed.
