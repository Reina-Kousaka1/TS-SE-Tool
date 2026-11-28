# Savegame parser and compatibility status

This document records evidence for the current Phase 2 savegame foundation. It distinguishes generic syntax handling from compatibility with actual ETS2/ATS files. No genuine game save or Coaches DLC fixture is present in the repository; the checked-in `tests/fixtures/synthetic/sample.sii` explicitly identifies itself as synthetic.

## Compatibility matrix

| Area | Current implementation | Evidence | Status |
| --- | --- | --- | --- |
| Euro Truck Simulator 2 | Read-only in-memory SII decoders exist, but no registered game adapter, version schema, profile reader, or game-specific validator exists. | Synthetic format tests only; no real ETS2 fixture. | **Not tested; game compatibility unsupported.** |
| American Truck Simulator | Same read-only in-memory decoder foundation; no registered game adapter, version schema, profile reader, or game-specific validator exists. | Synthetic format tests only; no real ATS fixture. | **Not tested; game compatibility unsupported.** |
| Savegame versions | A legacy-shaped synthetic `save_container` version field can be inspected; no game version or adapter is inferred or registered. | Legacy `SaveFileInfoData` uses UInt16; synthetic structural and bounds tests only. | **Synthetic field inspection only; real versions unsupported.** |
| Modified / modded saves | Raw field values and unclassified source lines are retained by the syntax document. The parser does not load mods, resolve mod schemas, or validate mod-specific meaning. | Synthetic unknown fields and trailer syntax are covered. | **Synthetic preservation confirmed; real mod saves not tested.** |
| Unknown SII attributes | Colon-delimited fields retain their original raw line and value. Unclassified lines are exposed as opaque source lines and block field editing. | Synthetic tests cover unknown field values and unclassified lines. | **Synthetic source preservation confirmed; semantic meaning unknown.** |
| Unknown object types | Block type and identifier text are retained without interpreting the object schema. | Synthetic unknown block type and nested block tests. | **Synthetic syntax preservation confirmed; real object layouts not tested.** |
| Block identifiers | Empty syntactic identifiers are rejected; non-empty identifiers remain raw text without game-specific grammar or reference validation. | Synthetic empty-identifier failure case; no genuine game identifiers. | **Basic syntax check confirmed; game identifier compatibility unsupported.** |
| Nested structures | Generic brace nesting, depth limits, and parent start locations are represented. No game-specific nesting rules are inferred. | Synthetic nested and same-line blocks. | **Generic syntax behavior confirmed synthetically.** |
| Vehicle / accessory references | A read-only inspector resolves an explicitly selected legacy-shaped synthetic array and reports missing, ambiguous and unknown targets; it does not infer categories or validate game semantics. Duplicate values are retained. | Synthetic array and reference tests. | **Synthetic inspection only; real reference validation unsupported.** |
| Profiles | No profile discovery or profile hierarchy reader is active. | Repository source and UI gates; no profile fixtures. | **Not tested; disabled.** |
| Money and XP editing | `LegacyProfilePreview` edits explicitly selected `bank.money_account` (Int64) and `economy.experience_points` (UInt32) in a synthetic plain-text document, in memory only. It is not connected to ScsC/BSII saves or game-specific profile discovery. | Synthetic adapter tests and historical legacy DTO mappings; no genuine save roundtrip. | **Synthetic editing only; real ETS2 mapping and write compatibility unverified.** |
| Trucks and trailers | No game-specific vehicle or trailer save models are implemented. | Repository model inventory; no real fixtures. | **Not tested; unsupported.** |
| Coaches DLC | No Coaches field names, references, or data model have been inferred. Generic block and field syntax can retain unknown text. | SCS confirms three licensed coach vehicles and describes coach ownership as separate from trucks while finances are shared ([release](https://blog.scssoft.com/2026/11/coaches-release.html), [ownership and jobs](https://blog.scssoft.com/2026/08/coaches-vehicle-ownership-job-schedule.html)). These are gameplay-level facts, not save-format evidence; no genuine Coaches fixture exists. | **Not tested; unsupported.** |

The in-memory fixture report counts repeated non-null block IDs across types
and repeated exact field names within each block. Indexed names are compared
literally. These are diagnostic hints, not schema validation; unclassified
syntax can make the counts incomplete. Counts are unavailable when parsing fails.
| Encoding and save containers | Read-only in-memory decoding now accepts plain `SiiN`, raw BSII v2/v3, and the AES-CBC/zlib `ScsC` wrapper under strict input/output limits. `ScsC` HMAC authentication is not checked; no container reconstruction or BSII serialization exists. | Synthetic byte-level tests only; no managed component has been tested against a genuine save. | **Container and game compatibility unverified; decoded `ScsC` data unauthenticated.** |

## Roundtrip guarantees

`SiiSyntaxDocument.ToText()` returns the exact original .NET string, including non-ASCII characters, line terminators, whitespace, comments, ordering, and a missing or present final newline. Synthetic regression tests assert string equality. This is a source-text roundtrip only; it does not establish that a game accepts the content or that a decoded save container can be reconstructed.

`WithFieldValue` changes one unique field value while preserving the original line prefix, trailing whitespace, and all other source lines. It reparses the candidate and checks block order, location, nesting, and parent relationships. It rejects edits when unclassified syntax exists, when the target is missing or ambiguous, or when the replacement changes the structure. Unknown colon-delimited fields remain in the document as raw values, but the parser cannot determine whether their semantics depend on an edited field.

No canonical serializer or format normalization is defined. The plain-text wrapper preserves the accepted BOM, encoding and bytes for an unchanged synthetic input. This does not establish byte equality for arbitrary encodings or game containers. The implementation makes no compression, number-format, reference, or game-version normalization.

## Bounded input and diagnostics

The parser applies configurable limits for total characters, lines, line length, blocks, fields, and nesting depth. `ParseReader` reads at most one character beyond the configured total-character budget and rejects an overlong line while scanning. Stable `SiiSyntaxErrorCode` values accompany parse exceptions, with line and column data when available. The encoded-text byte ceiling is 32 MiB; a synthetic 9 MiB document roundtrip guards against the previous 8 MiB ceiling. This covers the scale of a real ETS2 1.62 text dump reported in the external [format notes](https://github.com/Istarlabs/istar-truckdata/blob/78c382a72dcae02a50cbd175a45d6230409d721a/docs/save-game-format.md), but the managed parser has not been run against that save. The BSII decoder allows 32 MiB, 65,536 blocks, 65,536 items per array and 1,000,000 total block field values. Those limits exceed the cited example's 28,493 units; they remain defensive caps, not a claim that every real or modded save fits.

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
before comparing the captured bytes. Default output contains aggregate counts,
status flags, and `ByteIdentical` from exact comparison of the captured byte
arrays. The syntax difference counts ignore formatting and whole-line comments;
empty counts do not prove byte identity or game-semantic equivalence. The
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
and ambiguous input, exact versus formatting-only byte differences, and unchanged file bytes. Genuine compatibility remains
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
short header is reported as incomplete and other values as unrecognized.
`SiiFixtureInspector` still does not parse BSII: a header is not container
validation, a game version, or permission to decode. The separate managed
in-memory reader now supports bounded raw BSII v2/v3 payloads but remains
disconnected from this inspector and from encrypted/compressed game containers.
No source code from the MPL-2.0 project was incorporated. Its [test-data notice](https://github.com/TheLazyTomcat/SII_Decrypt/blob/d1cd7921d4667de895288c7227c58df43b63bd21/TestFiles/readme.txt)
states that player saves were withheld, so it provides no genuine fixture here.
The original `MethodsDecodeSave.cs` calls `SII_Decrypt.dll` twice and sets
`FileDecoded` after the second call without checking its result. The new
`LegacyDecodeSafetyCheck` does not invoke that DLL: it checks supplied status
codes and conservative size limits, then only reports syntax evidence after
rejecting malformed, opaque or ambiguous output. The size contract has been
partially probed with one synthetic native input as described below, but remains
unverified for genuine saves. No decoder is registered with the application or
enabled for profile access; these checks do not grant file-read or write permission.
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
contains synthetic integer fields and no game data. It runs only the known-hash
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

Managed binary-reader design self-review (not independent): the Windows-only
legacy DLL cannot serve the other target platforms. The managed reader now
parses the previously exercised historical v2 subset and documented BSII v3
in-memory payloads. For each field it retains
the exact encoded value bytes and the ordered field definition; vectors and
placements remain opaque raw payloads rather than being dropped. It covers the
documented string/token, scalar/list numeric, bool, ordinal, reference and
fixed-size vector/placement value codes. All counts and lengths are bounded,
unknown versions/types fail closed, and no partial result is returned. The
reader still does not decode the outer save container, serialize BSII, connect
to the desktop or read profile paths. The format description below was
independently implemented from the published
[SCS save-game format notes](https://github.com/Istarlabs/istar-truckdata/blob/78c382a72dcae02a50cbd175a45d6230409d721a/docs/save-game-format.md),
which report checks against real ETS2/ATS 2026 saves. That external evidence
does not validate this managed implementation. No source code or example save
bytes were imported. The notes' repository is LGPL-3.0-or-later; this project
uses the format description as a reference and does not incorporate its code.

`ExperimentalBinarySiiReader` accepts in-memory BSII versions 2 and 3. V2 is
restricted to the historical field types already covered by the legacy
synthetic probe; the expanded type table is enabled for v3. It
returns values only after a complete parse; block order, per-block field order,
field definitions, ordinal names, and each field's exact bytes are retained.
Types without an application-level interpretation (vectors, quaternions and
placements) are exposed as raw encoded data, not skipped. Version 3 may end at
EOF after a complete block; the historical synthetic v2 path retains its
explicit terminator behavior. Duplicate structure IDs, field names, ordinal
values and block identities, unresolved structure IDs, invalid UTF-8, unknown
types, oversized arrays, malformed identifiers and truncation fail without
partial output. Synthetic tests exercise the documented type codes and payload
sizes, but do not establish compatibility with actual saves or complete
container-level decoding. No binary writer or real profile access is enabled.

For historical type `0x27`, the original native decoder renders the raw value
`0xffffffff` as `nil` rather than a number. The managed value retains those raw
bits and separately exposes that legacy nil interpretation, avoiding a silent
numeric conversion. The locally executed native synthetic fixture confirms
`nil`, `0xfffffffe` and Int64 minimum output for the bundled x86 binary. This
does not prove that all versions or game fields use the same semantics.
The observation agrees with the original decoder's
[type 0x27 implementation](https://github.com/TheLazyTomcat/SII_Decrypt/blob/d1cd7921d4667de895288c7227c58df43b63bd21/Source/ValueNodes/SII_Decode_ValueNode_00000027.pas).
For type `0x33`, the same binary renders `nil` when either 32-bit half is
`0xffffffff`, even if the other half is not. The managed representation retains
all 64 raw bits and labels this only as legacy rendering behavior; it must not
silently replace the raw value with a null. Synthetic tests cover both mixed
halves, all bits set and an ordinary value, comparing the observable output of
the bundled x86 binary with the managed raw interpretation. Strings are
length-bounded, decoded as strict UTF-8 without normalization, and rejected
on invalid byte sequences. No game semantics or compatibility is inferred.
The experimental reader additionally supports historical string arrays
(`0x02`) and packed strings and arrays (`0x03`/`0x04`). It bounds array sizes,
checks every length and decodes each ordinary string as strict UTF-8. Packed
strings retain the complete 64-bit value, including the historically masked
high bit; an optional text interpretation can fail for unsupported digits and
must not be used as a unique identity. Empty, repeated and high-bit synthetic
values are exercised against the bundled x86 decoder; Unicode and malformed
UTF-8 cases are tested in the managed reader only. This is not a serializer or
evidence that current games use these fields identically.

The experimental reader also retains individual ID reference fields of
historical types `0x39`, `0x3b` and `0x3d`: the original type code, length or
special marker, and every 64-bit part are preserved. Null, nameless and a
two-part encoded reference are covered by an independently generated synthetic
fixture and the x86 decoder probe. An optional legacy-text interpretation fails
for unrepresentable components and never resolves ownership or target validity.
It masks the historically ignored high bit only for display; the raw bit remains
available, so identical display text must not be used as a unique identity.
Synthetic data-block IDs may be nameless or encoded; original marker and parts
are retained. Null or unrepresentable encoded block IDs are rejected. The
reader rejects duplicate legacy text identities, including collisions caused by
the masked high bit or between an encoded name and a nameless rendering. This
is a conservative ambiguity check, not a claim that the historical text form
proves game identity. The bundled x86 decoder and managed reader both accept a
constructed two-part named block, and managed tests cover collision and truncated
input. Real saves remain unverified. The reader also accepts synthetic reference arrays of types `0x3a`, `0x3c` and `0x3e`,
retaining order, duplicates and each element's raw marker and parts. A count
over 65,536 or truncated data fails before an array is returned. Empty and mixed
arrays of `0x3a`/`0x3c` are covered by the managed tests and bundled x86 DLL
probe; the newly documented `0x3e` type has managed synthetic coverage only.
This does not validate reference targets, real saves or game compatibility.

`ExperimentalBinaryReferenceIndex` provides an in-memory diagnostic over at
most 65,536 already parsed blocks. It snapshots raw identifiers and reports an
index only for one exact raw identity. Missing, null, unsupported, ambiguous,
and legacy-text-only references have distinct outcomes without a target index.
Matching identifiers do not establish field meaning, ownership, game version,
asset existence, or permission to modify a save. This diagnostic does not
discover, read, or write files and is not connected to a production save path.

Architecture and security self-review (not independent): masked-bit and
named/nameless display collisions cannot yield a target; synthetic tests cover
those cases, mutable input identifiers, malformed identifiers, and the block
limit. Actual-game compatibility and an independent review remain outstanding.

Historical types `0x05` and `0x06` expose individual Float32 values and bounded
arrays. The original IEEE-754 bits are retained independently of the floating
point view, including negative zero and NaN payloads; `IsFinite` is diagnostic
only and does not assert that a value is valid for fuel, wear or any game field.
The array limit is 65,536 entries, checked before allocation. Finite synthetic
values and negative zero are compared with the bundled x86 decoder; NaN and
infinity bit-preservation tests exercise only the managed reader. No semantic
game validation, binary writing or real-save support follows from these tests.

Historical byte-boolean scalar and array types `0x35` and `0x36` retain each
original byte. The separate legacy interpretation reports zero as false and
any nonzero byte as true; it does not replace the original byte with `1`.
Synthetic tests cover `0`, `1`, `2`, `255`, empty and oversized arrays and
truncated data. The known x86 binary confirms the corresponding legacy text for
the synthetic values. This is format-level evidence, not confirmation of
game-field meaning or permission to rewrite a save.

The experimental reader also recognizes bounded arrays of historical Int32,
UInt32, Int64 and UInt64 types. Unsigned array entries preserve raw bits and
expose the same legacy nil-rendering distinction as the corresponding scalars.
On fixed synthetic inputs the bundled x86 decoder agreed for Int32, UInt32 and
UInt64 array examples. A separate synthetic Int64-array example returned the
generic native error `-1` during its size query, both for integer boundary
values and for small values; the cause is not established. Int64-array support
therefore has managed synthetic tests only and remains unconfirmed against the
native decoder or game data. All numeric arrays are bounded to 65,536 entries
and reject truncation before allocating their result.

The upstream [change from 1.5.2 to 1.5.3](https://github.com/TheLazyTomcat/SII_Decrypt/commit/d1cd7921d4667de895288c7227c58df43b63bd21)
moved type `0x32` into the standard supported set; the predecessor's default
set excludes it. The bundled DLL identifies itself as 1.5.2.56, so that source
history is a plausible explanation for the observed `-1`, not proof of the
exact binary's source or build configuration. The fixed synthetic native probe
now asserts this rejection with default options. It does not enable the native
experimental unsupported-type option, decode a game save, or write output.

Array investigation self-review (not independent): isolated fixture variants
separated each array type while retaining the same existing synthetic fields;
only the Int64-array variant produced this observed error. No conclusion is
drawn about the native root cause or compatibility of real files. No write path
or game adapter is enabled.
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
