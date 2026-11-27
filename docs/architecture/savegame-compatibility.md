# Savegame parser and compatibility status

This document records evidence for the current Phase 2 savegame foundation. It distinguishes generic syntax handling from compatibility with actual ETS2/ATS files. No genuine game save or Coaches DLC fixture is present in the repository; the checked-in `tests/fixtures/synthetic/sample.sii` explicitly identifies itself as synthetic.

## Compatibility matrix

| Area | Current implementation | Evidence | Status |
| --- | --- | --- | --- |
| Euro Truck Simulator 2 | No game adapter is registered; no container decoder, version schema, profile reader, or game-specific validator exists. | Generic syntax tests only; no real ETS2 fixture. | **Not tested; game compatibility unsupported.** |
| American Truck Simulator | Same foundation as ETS2; shared SII syntax does not establish ATS compatibility. | Generic syntax tests only; no real ATS fixture. | **Not tested; game compatibility unsupported.** |
| Savegame versions | A legacy-shaped synthetic `save_container` version field can be inspected; no game version or adapter is inferred or registered. | Legacy `SaveFileInfoData` uses UInt16; synthetic structural and bounds tests only. | **Synthetic field inspection only; real versions unsupported.** |
| Modified / modded saves | Raw field values and unclassified source lines are retained by the syntax document. The parser does not load mods, resolve mod schemas, or validate mod-specific meaning. | Synthetic unknown fields and trailer syntax are covered. | **Synthetic preservation confirmed; real mod saves not tested.** |
| Unknown SII attributes | Colon-delimited fields retain their original raw line and value. Unclassified lines are exposed as opaque source lines and block field editing. | Synthetic tests cover unknown field values and unclassified lines. | **Synthetic source preservation confirmed; semantic meaning unknown.** |
| Unknown object types | Block type and identifier text are retained without interpreting the object schema. | Synthetic unknown block type and nested block tests. | **Synthetic syntax preservation confirmed; real object layouts not tested.** |
| Block identifiers | Empty syntactic identifiers are rejected; non-empty identifiers remain raw text without game-specific grammar or reference validation. | Synthetic empty-identifier failure case; no genuine game identifiers. | **Basic syntax check confirmed; game identifier compatibility unsupported.** |
| Nested structures | Generic brace nesting, depth limits, and parent start locations are represented. No game-specific nesting rules are inferred. | Synthetic nested and same-line blocks. | **Generic syntax behavior confirmed synthetically.** |
| Vehicle / accessory references | A read-only inspector resolves an explicitly selected legacy-shaped synthetic array and reports missing, ambiguous and unknown targets; it does not infer categories or validate game semantics. Duplicate values are retained. | Synthetic array and reference tests. | **Synthetic inspection only; real reference validation unsupported.** |
| Profiles | No profile discovery or profile hierarchy reader is active. | Repository source and UI gates; no profile fixtures. | **Not tested; disabled.** |
| Trucks and trailers | No game-specific vehicle or trailer save models are implemented. | Repository model inventory; no real fixtures. | **Not tested; unsupported.** |
| Coaches DLC | No Coaches field names, references, or data model have been inferred. Generic block and field syntax can retain unknown text. | No genuine Coaches fixture exists. | **Not tested; unsupported.** |

The in-memory fixture report counts repeated non-null block IDs across types
and repeated exact field names within each block. Indexed names are compared
literally. These are diagnostic hints, not schema validation; unclassified
syntax can make the counts incomplete. Counts are unavailable when parsing fails.
| Encoding and save containers | The in-memory plain-text wrapper accepts strict UTF-8 (with or without BOM) and BOM-marked UTF-16 LE/BE. It does not decode game compression or reconstruct binary containers. | Synthetic byte-level roundtrips only. | **Actual game-save encoding and containers unverified.** |

## Roundtrip guarantees

`SiiSyntaxDocument.ToText()` returns the exact original .NET string, including non-ASCII characters, line terminators, whitespace, comments, ordering, and a missing or present final newline. Synthetic regression tests assert string equality. This is a source-text roundtrip only; it does not establish that a game accepts the content or that a decoded save container can be reconstructed.

`WithFieldValue` changes one unique field value while preserving the original line prefix, trailing whitespace, and all other source lines. It reparses the candidate and checks block order, location, nesting, and parent relationships. It rejects edits when unclassified syntax exists, when the target is missing or ambiguous, or when the replacement changes the structure. Unknown colon-delimited fields remain in the document as raw values, but the parser cannot determine whether their semantics depend on an edited field.

No canonical serializer or format normalization is defined. The plain-text wrapper preserves the accepted BOM, encoding and bytes for an unchanged synthetic input. This does not establish byte equality for arbitrary encodings or game containers. The implementation makes no compression, number-format, reference, or game-version normalization.

## Bounded input and diagnostics

The parser applies configurable limits for total characters, lines, line length, blocks, fields, and nesting depth. `ParseReader` reads at most one character beyond the configured total-character budget and rejects an overlong line while scanning. Stable `SiiSyntaxErrorCode` values accompany parse exceptions, with line and column data when available. The default limits are defensive resource bounds; they have not been calibrated against genuine game saves.

The parser rejects unbalanced braces, blocks without headers, empty block identifiers, unclosed quoted values, input over configured bounds, and documents without blocks. Syntax that cannot be classified is retained and surfaced through `OpaqueLines`; a field edit is refused when such lines exist. The parser is a syntax foundation, not a complete SII grammar or game validator. The plain-text wrapper assumes BOM-less input is UTF-8, rejects invalid sequences, UTF-32 and NUL characters, and does not determine game-save encoding.

## Recovery preview boundary

Transaction self-review (not independent): the validator receives an isolated
copy of the proposed bytes; a mutation observed when it returns rejects the
transaction before staging. The independent write snapshot cannot be altered
through that validator buffer. This does not establish that a validator is
honest or game-compatible, and the transaction remains disconnected from saves.
The initial target check rejects directories and reports attribute errors
instead of treating them as absent files. Path-based checks remain non-atomic
and do not eliminate races with external filesystem changes.

`AtomicFileRecoveryPreviewer` reads the current target and recognized transaction artifacts, bounds content reads to the transaction payload limit, reports validation status and SHA-256, and can describe an exact-copy candidate targeting a separate output file. It performs no write, rename, delete, restore, or replace. Invalid or unavailable candidates have no proposed action. Validator exceptions become structured `payload_validation_failed` results; validator mutation is detected and prevents a valid recovery proposal.

The preview uses a caller-supplied validator. No game-specific validator is currently registered, and the Avalonia application does not point the previewer at production profiles. Any future recovery execution requires a separate independent architecture review and explicit operator authorization. This code does not itself implement or activate such execution.

## Verification remaining

`SiiFixtureInspector` prepares a read-only, in-memory report for explicitly
supplied bytes: bounded size, SHA-256, detected supported text encoding,
syntax diagnostics, unclassified-line count and exact byte roundtrip. The
report never asserts game compatibility, cannot open files, and does not return
the contents or a file path. Unsupported encodings and containers are reported
without attempting decompression or decryption. Real test copies require
explicit authorization; profile scanning and production writes stay disabled.

An opt-in developer tool in `tools/TSSE.FixtureInspector` can inspect exactly
one explicitly approved regular test-copy file, without directory scanning or
file writes. Run it only after authorization using
`dotnet run --project tools/TSSE.FixtureInspector -- --approved-test-copy <file>`.
The flag records the operator's assertion; it does not establish that the
file is a test copy. Output contains diagnostic metadata including a SHA-256,
not the file path or contents, and always marks game compatibility and save
writing as false. Unsupported containers are not decoded. The tool rejects
links using best-effort path checks; these are non-atomic and cannot prevent
concurrent path substitution. Run only on isolated, stationary copies. It is
not connected to Avalonia or profile discovery, and no recovery is performed.
For inputs within the byte limit, two bounded reads are compared by length and
SHA-256; a mismatch aborts without a report. Oversized inputs do not receive a
full-read consistency claim. Matching reads are evidence only, not an atomic
snapshot or a substitute for keeping the test copy stationary.

Fixture tool self-review (not independent): one explicit path, read-only open,
bounded reads via `SiiFixtureInspector`, no content/path in output, and no
compatibility or write authorization inferred from syntax. Synthetic tests
cover invalid arguments, unsupported prefixes, and unchanged file bytes.
The Windows x64 CI job also publishes a self-contained development copy,
executes it against the repository's synthetic fixture, and retains it as a
one-day Actions artifact for later authorized local inspection without a
local .NET 10 SDK. This is not a release or game compatibility proof. Other
platforms are not packaged by this development-only step.

`SiiSyntaxComparison` provides a separate in-memory comparison for explicitly
supplied parsed documents. It lists added, removed or moved blocks and added,
removed or changed fields; values are omitted, but names and IDs may themselves
be sensitive. It rejects duplicate IDs, duplicate fields, ambiguous anonymous
block identities and unclassified syntax. It compares trimmed raw values,
ignores source ordering and formatting, and reports only the block for added or
removed blocks. Therefore an empty result does not imply byte equality or game
semantic equality. The comparison itself performs no file access and is not wired
to profile discovery or the desktop. Tests use synthetic input only.

Architecture self-review (not independent): comparison requires two already
parsed bounded documents, matches globally unique IDs and unambiguous anonymous
block identities, checks parent identity, and rejects ambiguous syntax rather
than inferring matches. It is diagnostic evidence only; names and IDs must not
be logged or published without review. Real fixture compatibility remains open.

The developer-only fixture tool can compare two individually approved stationary
test copies with `--compare-approved-test-copies <before> <after>`. It opens
only those paths read-only, rejects linked paths, unsupported containers,
oversized, ambiguous or unclassified input, and verifies bounded read hashes
before comparing the captured bytes. Default output contains counts only; the
optional `--include-field-names` flag outputs at most 1,000 distinct field
names and a truncation indicator. Field names can be sensitive and require
review before sharing. Neither mode outputs field values, block IDs or paths;
paths passed on a command line can nevertheless be visible to the operating
system. Matching repeated reads are not atomic snapshots, the two files are not
read simultaneously, and the user must keep isolated copies stationary. The
tool cannot verify that a path is actually an approved copy. It does not scan,
decode containers, validate game semantics, or enable save writing.

Comparison-tool self-review (not independent): every compared document is
parsed from a third bounded read whose hash and length match the earlier two;
reparse-point checks are best effort and do not defeat every path race.
Synthetic tests cover privacy defaults, explicit name disclosure, unsupported
and ambiguous input, and unchanged file bytes. Genuine compatibility remains
unverified.

The Windows x64 CI smoke test also invokes the published standalone inspector
against two synthetic copies with one changed field, checking the default
count-only output and disabled game-compatibility and write flags. This checks
the packaged command path without establishing compatibility with game saves.

The report includes a four-byte signature hint for `SiiN`, `ScsC`, `BSII` and
`3nK` followed by byte `01`, as documented in the original SII Decrypt
[format constants](https://github.com/TheLazyTomcat/SII_Decrypt/blob/d1cd7921d4667de895288c7227c58df43b63bd21/Source/SII_Decrypt_Decryptor.pas).
The latter three prefixes are rejected as unsupported before text parsing. A
prefix is only a hint: it establishes neither validity nor game compatibility,
and BOM-prefixed or other forms can remain unknown. Tests use synthetic bytes;
there is no decoder, decryption, profile discovery or real fixture evidence.
For an eight-byte BSII header the report exposes the little-endian container
format version as a hint. Versions 1–3 are only the historical set described
in [SII Decrypt's format notes](https://github.com/TheLazyTomcat/SII_Decrypt/blob/d1cd7921d4667de895288c7227c58df43b63bd21/Documents/Binary%20SII%20-%20Format.txt);
that document itself describes the format as unofficial and incomplete. A
short header is reported as incomplete and other values as unrecognized. All
BSII inputs remain unsupported: a header is not container validation, a game
version, or permission to decode. No source code from the MPL-2.0 project was
incorporated. Its [test-data notice](https://github.com/TheLazyTomcat/SII_Decrypt/blob/d1cd7921d4667de895288c7227c58df43b63bd21/TestFiles/readme.txt)
states that player saves were withheld, so it provides no genuine fixture here.
The original `MethodsDecodeSave.cs` calls `SII_Decrypt.dll` twice and sets
`FileDecoded` after the second call without checking its result. The new
`LegacyDecodeSafetyCheck` does not invoke that DLL: it checks supplied status
codes and conservative size limits, then only reports syntax evidence after
rejecting malformed, opaque or ambiguous output. The size contract has been
partially probed with one synthetic native input as described below, but remains
unverified for genuine saves. No decoder is
registered or enabled; these checks do not grant file-read or write permission.
The separate size-query check can be called before allocation and returns a
bounded integer only on a successful query with a nonzero size within the
existing text limit. This does not invoke the native decoder.
The referenced [native header](https://github.com/TheLazyTomcat/SII_Decrypt/blob/d1cd7921d4667de895288c7227c58df43b63bd21/Library/SII_Decrypt_Library_Header.pas)
describes a pointer-sized `TMemSize`, a minimum buffer size from the first
call, and the actual data size from the second. The check retains 64-bit sizes,
accepts a bounded larger buffer, and inspects only the reported output bytes.
This source is not proof that the repository's bundled DLL has the same ABI;
the legacy C# `uint*` declaration must not be reused for a 64-bit native call
without verifying that exact binary and its calling convention.
Static inspection of the repository's `TS SE Tool/SII_Decrypt.dll` found PE32
I386 (x86), file version 1.5.2.56, SHA-256
`F6393C253BBBBC8DDB7B2F39A22A9223FB2C80D927657B077B0DBC92532BFEBA`.
This binary does not match an x64 or ARM64 process. `NativeDecoderHeaderProbe`
checks bounded PE headers without loading native code; an architecture match
does not establish ABI, exports, OS compatibility, trust, or authorization.
No 64-bit replacement is assumed.
An additional bounded `NativeDecoderExportProbe` inspects the PE export table
without loading the image. The bundled binary has named non-forwarded RVAs for
`GetMemoryFormat`, `DecodeMemory` and `DecryptAndDecodeMemory`; malformed or
oversized tables are rejected. The table layout and forwarder distinction follow
the [Microsoft PE specification](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format).
An RVA may point to code or data: this evidence does not confirm a callable
function, calling convention, parameter sizes, implementation, trust, or
platform compatibility. No native calls or container decoding are enabled in
the application.
Export-probe self-review (not independent): input and table counts are bounded,
names are checked as terminated ASCII, ordinal indexes are range-checked, and
forwarded entries are distinguished. Synthetic corruption tests and the checked-in
legacy binary exercise inspection only; ABI review and isolated decoder tests
remain necessary.

The development-only `tools/TSSE.NativeDecoderProbe/Test-GetMemoryFormat-x86.ps1`
uses a separate 32-bit Windows PowerShell process, verifies the known SHA-256,
and calls only `GetMemoryFormat` with three fixed synthetic in-memory buffers.
Locally on Windows the bundled DLL returned plaintext=1, unknown=10 and
too-short=11. The Windows x64 CI runs the same isolated-process check. The
script takes no file paths or save data and does not call a decode or write
function. These three outcomes support only the tested x86 format-query
signature; they do not validate a decoder, prove absence of native side
effects, establish binary provenance or game compatibility, or authorize
loading it in the application. Hash verification and dependency search
restrictions are best effort, not a security sandbox. A reviewed isolation
design and real approved test-copy evidence remain required.

`tools/TSSE.NativeDecoderProbe/Test-DecodeSynthetic-x86.ps1` constructs a fixed
binary fixture in memory from the documented historical BSII v2 structure; it
contains one synthetic Int32 field and no game data. It runs only the known-hash
x86 DLL in a separate process, makes a bounded output-size query, caps allocation,
and validates returned length, UTF-8, and the expected structure and field.
Locally, the full-buffer call produced the expected text. Crucially, calls with
declared output capacities of zero and one returned success with reported sizes
zero and one respectively; the one-byte case did not write beyond its declared
capacity in this test. Therefore a success code alone does not establish complete
output. The existing `LegacyDecodeSafetyCheck` rejects zero-length and malformed
output, but syntactically valid truncation is still possible; no production
decoder or write path is enabled. Windows x64 CI repeats this fixed synthetic
test with a two-minute step timeout. The process and hash checks do not constitute
a sandbox, the historical format description is unofficial, and no genuine save,
encryption, game semantics or save reconstruction was tested. The generated
fixture is not copied from a player or from the external documentation example.

Synthetic decoder self-review (not independent): native input is generated from
constants, no arguments or game files are accepted, and no output is written to
a file. The experiment cannot prove memory safety, origin or ABI compatibility
across builds. Any future application integration needs explicit isolation,
completeness checks, real approved fixtures and separate security review.
The Avalonia Compatibility Lab now exposes this report through fixed
synthetic examples, separating syntax status, prefix hints, byte-roundtrip and
structured syntax errors. It accepts no paths, pasted saves or external data.
The display is a demonstration of diagnostics, not a compatibility verdict.

For an additional synthetic legacy metadata example, a scoped inspector reads
only a unique direct `save_container` under a unique `SiiNunit` wrapper and
its unique positive UInt16 `version` field. Unclassified syntax, duplicate or
misplaced containers, missing fields and invalid values return explicit statuses.
The legacy `SaveFileInfoData` class is the source for the field and type; this
does not identify a game, register an adapter, or establish compatibility.

An in-memory diagnostic can match a successfully read legacy field against an
explicitly supplied game ID and the exact decimal version key in the existing
registry. A missing or invalid version skips the lookup. The result is not an
authorization: its game-save read and write permissions remain false, including
for a registry match. Tests register a fictitious game solely to exercise that
boundary; no real adapter is registered. The probe opens and changes no files.

Compatibility self-review (not independent): the game ID is caller-supplied,
not inferred; numeric formatting is invariant; unknown versions remain unknown.
A registry match does not prove that a file is complete, that its container was
decoded correctly, or that a game would accept it. Additional validation and a
separate explicit approval are still required before enabling writes.

The inspector also accepts a caller-owned stream and reads no more than the
byte limit plus one detection byte; an oversized stream's reported length is
marked as a lower bound. Read errors propagate rather than becoming success
reports. It neither opens paths nor closes the caller's stream. This API does
not activate a file picker, automatic profile discovery or game-save reading.

Fixture-inspection self-review (not independent): the byte limit is checked
before hashing, strict decoding precedes parsing, syntax errors are structured,
and an unchanged byte comparison is reported separately from parsing. A clean
report is not a complete SII grammar check, a game-version assessment or an
in-game load test. Fixture provenance, privacy and licensing remain necessary.

Before claiming ETS2, ATS, mods, a save version, or Coaches DLC compatibility, add safely sourced genuine fixtures with provenance and licensing, implement the relevant container decoding and version-aware validation, and demonstrate unchanged roundtrips plus in-game or authoritative format checks. Keep profile scanning and writes disabled until those checks exist and the security gates are separately reviewed.
