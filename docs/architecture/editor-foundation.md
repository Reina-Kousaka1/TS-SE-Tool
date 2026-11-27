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
is connected to this demo. The vehicle and job demos also use synthetic data;
real profile mapping and game compatibility remain future work.

CI evidence for core commit 29add2b: run 45980964128 passed its .NET 10 test
steps on all seven jobs. Six jobs completed successfully; the Linux x64 job
failed later while refreshing the Microsoft Ubuntu package feed (HTTP 403),
before its package smoke tests. This is an external feed failure, not evidence
that packaging passed.

The legacy application's Bank.cs represents money_account as Int64 and
Economy.cs represents experience_points as UInt32. This informs the isolated
value ranges only; it does not prove compatibility with current game versions.
Negative balances are accepted; experience is unsigned. Input parsing is
invariant decimal without whitespace or thousands separators. A legacy level
calculator uses the two threshold tables found in the original FormMethods.cs;
the synthetic profile demo lets users select either table and set the XP just
below a requested next level, matching the legacy level-setting behavior for
levels 0–150. It uses wider arithmetic to avoid overflow at UInt32.MaxValue.
These tables are not verified against current game versions. No file is changed.

Level self-review (not independent): table selection is explicit, calculations
do not change XP until the user applies a level, and the SII edit stays within
the existing in-memory session. Values beyond level 150 are rejected rather
than silently clamped. No compatibility or write gate is changed.

## In-memory SII edit batches

An independent plain-text byte wrapper supports strict UTF-8 and BOM-marked
UTF-16 LE/BE, retaining the original BOM and encoding. It checks syntax,
encoding and byte limits before returning an edited value. It has no file I/O
and is not a decoder for compressed or encrypted game saves.

`SiiEncodedEditSession` combines this wrapper with the bounded SII edit
history. Every candidate is encoded and checked before history changes;
undo/redo returns the corresponding bytes with the original BOM and encoding.
It operates only on caller-supplied in-memory bytes and remains disconnected
from real save scanning and file writes.

Encoding self-review (not independent): malformed bytes and unpaired
surrogates are rejected rather than replaced; unchanged synthetic inputs are
tested for byte equality. BOM-less UTF-16, UTF-32, arbitrary legacy encodings
and actual game-save container compatibility remain unsupported or unverified.
Encoded-session self-review (not independent): candidate validation runs before
the underlying history is changed; tests cover rejected batches, redo
preservation, unknown Unicode fields and BOM-preserving undo/redo.

`SiiEditSession` stages explicit field replacements against an already parsed
syntax document and groups each batch into one undo entry. A failed replacement,
duplicate target, or unclassified syntax leaves the current text and redo history
unchanged. Unchanged replacements do not create history entries. This is a
syntax-level facility for synthetic tests; it has no file access, save-version
validation, or mapping from profile models to real game files. The UI profile
demo uses only embedded synthetic text. Unknown recognized fields and blocks remain in the
original text; unclassified syntax blocks edits.

The in-memory session reports net changes against its initial syntax document,
including original and current field values (surrounding whitespace trimmed) in
source order. The synthetic
profile, vehicle and job views show this list and refresh it on undo/redo.
Returning a field to its original value removes it from the list; undo history
may still exist. This is a syntax diff, not game-semantic validation.

Change tracking self-review (not independent): only targets from successfully
applied batches enter the tracking set, and both versions are read from the
immutable syntax snapshots. No logs or filesystem output are added. The views
currently show only embedded synthetic data; any future real-data integration
must consider visibility and privacy of displayed field values.

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

## Experimental vehicle mapping

`LegacyVehiclePreview` edits selected fuel and wear ratios in explicitly
selected synthetic `vehicle` and `trailer` blocks. Field names are based on
legacy Vehicle.cs and Trailer.cs. Both decimal and `&` plus eight hexadecimal
IEEE-754 bit patterns are read; non-finite or out-of-range values fail closed.
Changed values are emitted as invariant round-trip decimals. Unchanged values
retain their original text. Only an explicitly selected field is edited; wheel
arrays, references and unknown fields are preserved, not interpreted.
This does not claim full repair, game compatibility, or Coaches support.
The Vehicle Studio exposes a labelled demo using two independent embedded
synthetic sessions with field selection and undo/redo. It has no file access.
Selecting Coaches hides the demo; no Coach model is inferred from truck fields.
The demo can reset listed truck mechanical/body wear or trailer body/chassis
wear in one undoable batch, and can refill the demo truck's fuel separately.
These operations do not repair wheels, cargo, accessories or linked trailers.
Any missing or invalid required wear field rejects the complete batch.
An additional independent demo operation validates a wheel array's declared
count and exact indices, then sets existing values to zero as one undoable
batch, preserving the count and other fields. Arrays over 64 entries, malformed
or duplicated indices and invalid values fail closed. This differs from the
legacy application's array-clearing behavior and remains synthetic-only.

Architecture self-review (not independent): field allowlists prevent applying
trailer edits to trucks and vice versa. Unique block and field checks prevent
ambiguous replacements; existing syntax session safeguards remain active.
Valid decimal encoding and the ratio range for real game versions remain
unverified until genuine fixture and in-game tests are performed.

UI self-review (not independent): each category has a separate synthetic
session and history, input is restricted to invariant ratios, and no path or
save command is introduced. Users can change cargo damage in the trailer demo;
this is not represented as automatic cargo repair.

Repair self-review (not independent): all required scalar values are validated
before the batch is submitted, the existing syntax session publishes it
atomically, and the UI identifies the exclusions. This is not equivalent to the
legacy tool's complete repair, which also handles wheels and linked trailers.
Wheel self-review (not independent): all elements are validated before publishing
the batch; the fixed entry limit bounds repeated syntax reparsing. No traversal
of linked trailers or alteration of accessories is performed.

An explicit trailer-chain demo action follows the legacy `slave_trailer`
reference and resets only body and chassis wear in all visited synthetic
trailers as one undoable batch. Cargo, wheels and accessories are unchanged.
Missing or duplicate fields and targets, invalid ratios, cycles, or chains
longer than 64 trailers reject the entire action.

Chain self-review (not independent): traversal and validation complete before
the existing atomic syntax session is updated. The demo does not establish
compatibility with real trailer relationships or write any files.

## Experimental active job mapping

`LegacyJobPreview` follows an explicitly selected synthetic player block's
`current_job` reference to a unique `player_job` block. The legacy Player,
Player_Job and SiiNunit classes provide the names and Int32 units field.
The cargo reference is displayed as raw data, and `units_count` may be
changed as a nonnegative integer in memory. The separately selected
`planned_distance_km` field is also editable as a nonnegative Int32, based
on the legacy Player_Job model; it does not recompute routes or payment.
A missing, invalid or ambiguous
target fails closed. `null` and `nil` mean no active job in this preview.
The operation does not update other job, cargo or trailer data, and therefore
does not establish game-level consistency or compatibility. The Jobs page
connects it to an embedded synthetic example; it has no file operations.

Architecture self-review (not independent): job selection is fixed to the
original reference, editing only changes a single validated integer, and the
existing syntax transaction preserves unknown fields and supports undo/redo.
Actual relationships, counts, game versions and online jobs require real
fixtures and in-game validation before any production use.

UI self-review (not independent): the Jobs page constructs a constant synthetic
document, labels it as a demo, and has no file or save command. The visible
warning states that changing units or distance does not validate cargo, route
or trailer relations.

## Architecture self-review (not independent)

Decision: keep the editor core detached from file transactions and SII until
game-specific validation is established. History relies on immutable values;
using a mutable object would invalidate undo guarantees. ProfileValues is an
immutable record. SII preview documents are capped at 2 Mi characters and
16 history states, including the original; generic histories default to 128
states. At the limit, the next edit is explicitly rejected without dropping
undo or redo; undo followed by a different edit can reclaim the redo branch.
The character/state caps bound retained source text, not total process memory:
parsed objects and transient reparsing require additional memory.
Compatibility, persistence, model-to-SII mapping and independent recovery
review remain open; no write feature gate has been changed.
