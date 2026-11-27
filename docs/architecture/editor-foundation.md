# Editor foundation

The editor session in TSSE.Core is an in-memory foundation. It cannot discover,
open, serialize, or write a game save. ProfileValues holds only balance and
experience as detached values; it is not a complete profile or a versioned SII
mapping. A new session has an original baseline. Valid edits create snapshots,
undo and redo move through them, and a different edit after undo drops redo.
The synthetic profile UI can apply balance and XP together after validating
both; stale drafts require an explicit reviewed action and the pair forms a
single undo step. This remains an in-memory exercise, not save editing.
Unchanged or invalid edits leave history intact. Callers of the generic history
must supply immutable snapshots and a validator.
The generic history compares the current snapshot to its original using the
snapshot type's default equality comparer; a manual return to the original
clears dirty status without removing undo/redo entries. Snapshot types must
therefore implement equality appropriate to their editing semantics.
The profile and SII editing sessions report dirty state relative to the
original values or exact original text respectively, even if a later edit
returns to that baseline. This does not discard undo or redo history. Textual
changes such as `001` to `1` remain dirty even when numerically equivalent.

The Savegame Explorer offers a clearly labelled temporary profile demo for
balance and experience, with per-field validation and undo/redo. Its initial
zeroes are scratch values, not data read from a profile. The demo edits and
displays an embedded synthetic SII document in memory. No loading or saving
is connected to this demo. The vehicle and job demos also use synthetic data;
real profile mapping and game compatibility remain future work.

The explorer also contains a separate synthetic `user_profile` metadata example.
The legacy `SaveFileProfileData.cs` names `profile_name`, `cached_experience`,
and `cached_distance`; this preview reads the two cached UInt32 values and can
change the example name. It accepts a conservative subset of Unicode names and
legacy-shaped UTF-8 `\xNN` escapes, with a 64 UTF-16-code-unit limit. It rejects
invalid UTF-8, controls, unsupported punctuation and unknown escapes. The
subset and limit are preview restrictions, not claims about the game. Its independent
undo/redo history never renames a profile directory, updates game data, scans
profiles, or writes files. It rejects unknown string formats, ambiguous fields
and invalid numeric values, while preserving unknown syntax fields. Current
game compatibility is not verified. In contrast to the legacy code's per-char
encoding, supplementary Unicode characters are encoded as complete UTF-8;
this is synthetic behavior and is not evidence of game acceptance.

Metadata self-review (not independent): this is a separate in-memory document
and history, rather than conflating profile metadata with the game save. Only
one explicit field can be edited through the existing syntax session; cached
statistics are read-only. Actual profile renaming would need directory,
cross-file and game-version validation and remains disabled.

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

An edit with a non-null target ID fails closed when the ID is repeated across
block types. Batch failure preserves the original preview and redo history.
This conservative synthetic syntax rule does not establish game semantics.

The synthetic truck demo exposes the `odometer` UInt32 field declared by the
legacy `Vehicle` model. Its separate `odometer_float_part` is retained exactly
and is not recalculated. The editor rejects missing, duplicated, malformed and
out-of-range integer values; undo/redo and stale-draft protection apply. The
unit, the relationship to the fractional part, and current game compatibility
are not verified. This is an in-memory preview, not a game-save operation.
The legacy trailer model also declares these two fields; the same isolated
integer operation and the existing odometer UI now support explicitly selected
synthetic trailers. Validation against genuine trailer saves remains open.

Odometer self-review (not independent): only the explicitly selected vehicle
integer is changed, the syntax session preserves all other source lines, and a
trailer is rejected. No version or unit inference and no write gate are added.

For synthetic wear and fuel ratios, decimal input is checked in double
precision, and the decimal digit sequence is checked against the exact [0, 1]
boundary, before conversion to the legacy single-precision value. Values that
underflow from a nonzero decimal to zero are rejected; this avoids silently
accepting a tiny negative value or erasing a tiny positive value. This remains
floating-point conversion with a 1024-character input limit, not a general
arbitrary-precision decimal implementation.

`LegacySyntheticWorkspace` combines explicitly selected profile, truck,
trailer and job blocks in one in-memory syntax session. Changes in different
domains share a single ordered undo/redo history and change list. It does not
discover profiles, resolve ownership automatically, or access files. Its
constructor requires the relevant synthetic block identifiers; no game
compatibility is implied. The profile, vehicle and job demos share one
workspace and label undo/redo as cross-section operations. Draft input is
preserved when another section changes the underlying synthetic document.
The demo's synthetic trailer block identifiers are listed from its in-memory
document in source order. Missing or duplicate identifiers fail the listing;
the list does not infer ownership or game semantics.
Synthetic truck blocks are also selectable by identifier in document order.
Fuel/wear drafts and integer odometer drafts are kept separately for each
truck, retain their baseline across selection, and require review when stale.
The shared history spans all selected trucks and trailers.
The synthetic player demo can explicitly change `assigned_truck`, which the
legacy Player model and truck tab both use. Before changing it, the demo
validates the complete declared `trucks` array, uniqueness, target block type,
and the current reference. Only an existing listed target may be selected;
the reference change participates in shared undo/redo. It does not adjust or
validate placement, active jobs, ownership semantics or real-game versions.
The same bounded reference validation is used for the synthetic
`assigned_trailer` field and the player's `trailers` array. The action changes
only that reference and does not attach or detach a trailer, update placements,
or validate jobs or real-game versions. Missing and ambiguous references fail
closed; shared undo/redo includes the assignment.

Assignment self-review (not independent): all syntactic reference checks run
before the existing atomic edit. No profile discovery or game-save access is
added, and the separate production-write gate remains closed.
Synthetic trailer-chain repairs also reject a chain ID shared with any other
block type, including at the first or a later link. Validation precedes the
single batch, so an ambiguous link leaves the preview and history unchanged.
The synthetic active-job preview likewise requires its player and job IDs to
be unique across block types before exposing job edits.
The shared reference-array reader applies the same source-ID check before
inspecting synthetic player and accessory arrays.
The synthetic assignment validator indexes only referenced block IDs in one
document pass, still rejecting duplicate IDs across all block types. The shared
bounded array reader indexes field names once and preserves numeric index order.
Large synthetic inventory tests cover ordering and a late ambiguous reference;
they are not a benchmark or evidence of real-save performance.
For the selected synthetic truck, the full repair action validates engine,
transmission, cabin, chassis and every declared wheel before a single undoable
change. Fuel, wheel count and unknown fields remain intact. This mirrors the
scope of the original repair control without deleting the wheel array; it does
not establish compatibility with actual game data.
The vehicle demo separately inspects the synthetic player's legacy-shaped
`trailers` reference array. It reports missing, ambiguous, empty, differently
typed, and duplicate references without changing the selection or document.
Array counts and indices are validated using the same bounded reader as the
existing accessory inspector. The report does not establish actual ownership
or validate the real game format.
Drafts are scoped to trailer identity and field; chain operations begin at the
selected trailer. The selection does not discover or enumerate game profiles.
UI workspace self-review (not independent): the catalog owns the shared demo
and its view models for the same lifetime. Notifications refresh previews,
change lists and committed values; a text box that differs from its last
committed value keeps its draft. Undo and redo are global, and are labelled as
such in the UI. Vehicle drafts persist across field and category selection;
if the underlying value changes, the user sees the current value and must
explicitly apply the reviewed stale draft.

Draft self-review (not independent): drafts are keyed by vehicle category and
field, stay in memory, and do not modify the document until explicitly applied.
A stale draft cannot use the normal apply action; the separate reviewed action
is intentional. Game-save editing and file access remain disabled.
Profile balance/experience and job units/distance use the same explicit review
principle when a shared undo or another change alters the committed value.
Setting a demo level leaves an unrelated experience draft intact and flags it
when the committed XP changes.

Workspace self-review (not independent): only typed preview operations can
modify the shared session; those operations do not change the profile or job
references captured during construction. Shared undo and redo apply to the
whole document, rather than just the selected domain. Unknown fields remain
in the original syntax. Real-save ownership and cross-field validation remain
open before any production integration.

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

The synthetic profile skills can be reset together as one undoable edit.
All six existing values are validated before the batch; any missing, duplicate
or unsupported value rejects it. The UI blocks this action while any skill
draft is open. This is an in-memory demo, not a game-save operation.

The synthetic profile demo exposes the legacy `adr`, `long_dist`, `heavy`,
`fragile`, `urgent`, and `mechanical` fields. The original Economy model uses
bytes, while the original skill controls expose six ADR bits and five six-step
controls. The preview accepts ADR 0–63 and other skills 0–6, refuses missing,
duplicate or out-of-range original values, preserves unknown fields, and uses
the shared undo history. Per-field drafts survive selection and require review
after an underlying value changes. Skill-to-XP relationships, bit meanings,
current versions and game compatibility remain unverified; no files are opened.
The six synthetic ADR checkboxes manipulate only the draft mask; the first
corresponds to the most significant of the six bits, as in the original
control. Invalid masks disable the checkboxes. Applying still requires the
explicit button and stale drafts require review; no bit-to-cargo-class mapping
is inferred.

Skill self-review (not independent): edits target exactly one existing legacy
field, reject unsupported values instead of truncating bits, and inherit the
in-memory syntax transaction. UI labels identify the data as synthetic.

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

The legacy vehicle and trailer models declare `license_plate` as `SCS_String`.
The synthetic preview exposes its original raw field value read-only, without
decoding, validating, or editing a plate. Missing or duplicated fields are
reported as unavailable. The legacy encoding and actual save compatibility
are not verified; there is no production profile access or write path.

`LegacyAccessoryInspector` inspects the legacy-shaped `accessories` array of
an explicitly named synthetic block. It preserves ordering and duplicate
references and reports missing, ambiguous, empty, known-type and unknown-type
targets. Invalid counts and indices fail closed. It does not classify a block
as a truck, trailer or coach, and introduces no Coaches-specific fields.

Accessory self-review (not independent): the inspector is read-only and keeps
unknown block types and raw references visible. Its generic selector enables
future schema work without asserting that a coach has any particular field.
Real reference formats, ownership and DLC compatibility remain unverified.
Vehicle Studio presents this inspection for embedded truck and trailer
examples, including a missing and an unknown target. The display is read-only
and is labelled as synthetic; selecting Coaches does not infer accessory data.

`LegacyVehiclePreview` edits selected fuel and wear ratios in explicitly
selected synthetic `vehicle` and `trailer` blocks. Field names are based on
legacy Vehicle.cs and Trailer.cs. Both decimal and `&` plus eight hexadecimal
IEEE-754 bit patterns are read; non-finite or out-of-range values fail closed.
Changed values are emitted as invariant round-trip decimals. Unchanged values
retain their original text. Only an explicitly selected field is edited; wheel
arrays, references and unknown fields are preserved, not interpreted.
This does not claim full repair, game compatibility, or Coaches support.
The Vehicle Studio exposes a labelled demo using a shared embedded synthetic
session with field selection and undo/redo. It has no file access.
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

UI self-review (not independent): both categories share synthetic
session history, input is restricted to invariant ratios, and no path or
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

The synthetic chain demo also exposes a full repair action for body, chassis,
cargo and existing wheel values. Every link and field is validated before one
atomic history entry; wheel counts and array entries are retained instead of
being removed as in the original application. The action starts at the chosen
demo trailer. It does not validate game-level cargo or trailer relationships.

## Experimental active job mapping

The synthetic job demo permits a nonnegative `cargo_model_index` integer,
declared by the legacy `Player_Job` model. It checks the existing field and
shares undo/redo and stale-draft safeguards. It does not resolve or validate
cargo/model relationships or establish compatibility with a game version.

The synthetic job preview also selects an existing `cargo` reference observed
in explicitly parsed `player_job`, `job_info`, or `job_offer_data` blocks.
The legacy source defines these fields and block types; the synthetic catalog
restricts references to an ASCII `cargo.` subset and rejects malformed or
duplicate fields. This is a syntax-only candidate list, not evidence that a
cargo is available, belongs to the job, or fits a trailer. Only the one raw
reference changes, with shared undo/redo and stale-selection review. No game
file access or production cargo editing is enabled.

Cargo self-review (not independent): candidates are reused from the current
in-memory document and revalidated immediately before the edit; missing,
ambiguous or unsupported fields fail without changing history. Unvalidated
cross-field relationships and real save formats remain blockers.

`LegacyJobPreview` follows an explicitly selected synthetic player block's
`current_job` reference to a unique `player_job` block. The legacy Player,
Player_Job and SiiNunit classes provide the names and Int32 units field.
The cargo reference is displayed as raw data, and `units_count` may be
changed as a nonnegative integer in memory. The separately selected
`planned_distance_km` field is also editable as a nonnegative Int32, based
on the legacy Player_Job model; it does not recompute routes or payment.
The synthetic Jobs page can also apply both values as one atomic history
entry. Both existing values and both input values must be valid; stale drafts
block the combined action. Undo restores both fields together.
A missing, invalid or ambiguous
target fails closed. `null` and `nil` mean no active job in this preview.
The synthetic job page displays and can swap the existing `source_company` and
`target_company` raw values in one undoable edit. Missing, duplicate or empty
references fail closed; equal references produce no history entry. The legacy
`Player_Job` model declares both fields. No geographic, payment, placement or
cargo relationships are recomputed or validated by this synthetic operation.
The operation does not update other job, cargo or trailer data, and therefore
does not establish game-level consistency or compatibility. The Jobs page
connects it to an embedded synthetic example; it has no file operations.

Architecture self-review (not independent): job selection is fixed to the
original reference, editing changes only explicitly selected validated integers, and the
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
The profile demo offers a reset of all applied changes in the shared synthetic
workspace. It restores the exact initial text as one undoable history entry;
unapplied input drafts remain governed by the existing stale-draft checks. The
reset does not read or write files. If the history is full it fails without
changing the preview. A no-op reset leaves redo history intact.

Architecture self-review (not independent): the reset reuses the immutable
initial syntax snapshot and existing history limit and candidate validation.
Regression tests cover cross-section notification, exact text restoration,
undo/redo, no-op redo preservation, and history exhaustion. Real save support
and permission to write remain separate unresolved requirements.
