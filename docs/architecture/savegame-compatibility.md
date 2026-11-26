# Savegame parser and compatibility status

This document records evidence for the current Phase 2 savegame foundation. It distinguishes generic syntax handling from compatibility with actual ETS2/ATS files. No genuine game save or Coaches DLC fixture is present in the repository; the checked-in `tests/fixtures/synthetic/sample.sii` explicitly identifies itself as synthetic.

## Compatibility matrix

| Area | Current implementation | Evidence | Status |
| --- | --- | --- | --- |
| Euro Truck Simulator 2 | No game adapter is registered; no container decoder, version schema, profile reader, or game-specific validator exists. | Generic syntax tests only; no real ETS2 fixture. | **Not tested; game compatibility unsupported.** |
| American Truck Simulator | Same foundation as ETS2; shared SII syntax does not establish ATS compatibility. | Generic syntax tests only; no real ATS fixture. | **Not tested; game compatibility unsupported.** |
| Savegame versions | No save-version detection or version-specific parser registration exists. | Synthetic test text contains no game version claim. | **Not tested; unsupported.** |
| Modified / modded saves | Raw field values and unclassified source lines are retained by the syntax document. The parser does not load mods, resolve mod schemas, or validate mod-specific meaning. | Synthetic unknown fields and trailer syntax are covered. | **Synthetic preservation confirmed; real mod saves not tested.** |
| Unknown SII attributes | Colon-delimited fields retain their original raw line and value. Unclassified lines are exposed as opaque source lines and block field editing. | Synthetic tests cover unknown field values and unclassified lines. | **Synthetic source preservation confirmed; semantic meaning unknown.** |
| Unknown object types | Block type and identifier text are retained without interpreting the object schema. | Synthetic unknown block type and nested block tests. | **Synthetic syntax preservation confirmed; real object layouts not tested.** |
| Nested structures | Generic brace nesting, depth limits, and parent start locations are represented. No game-specific nesting rules are inferred. | Synthetic nested and same-line blocks. | **Generic syntax behavior confirmed synthetically.** |
| Vehicle / accessory references | Reference-looking values remain raw field values. The parser does not resolve IDs, detect dangling references, or infer vehicle/accessory schemas. Duplicate values are retained. | Synthetic duplicate-reference and raw-value tests. | **Raw preservation confirmed synthetically; reference validation unsupported.** |
| Profiles | No profile discovery or profile hierarchy reader is active. | Repository source and UI gates; no profile fixtures. | **Not tested; disabled.** |
| Trucks and trailers | No game-specific vehicle or trailer save models are implemented. | Repository model inventory; no real fixtures. | **Not tested; unsupported.** |
| Coaches DLC | No Coaches field names, references, or data model have been inferred. Generic block and field syntax can retain unknown text. | No genuine Coaches fixture exists. | **Not tested; unsupported.** |
| Encoding and save containers | Parser accepts .NET text. It does not decode game compression, detect original byte encoding, or reconstruct a binary save container. | Unit tests operate on synthetic strings/readers. | **Byte-level compatibility unsupported.** |

## Roundtrip guarantees

`SiiSyntaxDocument.ToText()` returns the exact original .NET string, including line terminators, whitespace, comments, ordering, and a missing or present final newline. Synthetic regression tests assert string equality. This is a source-text roundtrip only; it does not establish that a game accepts the content or that a decoded save container can be reconstructed.

`WithFieldValue` changes one unique field value while preserving the original line prefix, trailing whitespace, and all other source lines. It reparses the candidate and checks block order, location, nesting, and parent relationships. It rejects edits when unclassified syntax exists, when the target is missing or ambiguous, or when the replacement changes the structure. Unknown colon-delimited fields remain in the document as raw values, but the parser cannot determine whether their semantics depend on an edited field.

No canonical serializer or format normalization is defined. Byte equality is not promised because the current parser starts from text and has no byte/container decoder. The implementation makes no encoding, compression, number-format, reference, or game-version normalization.

## Bounded input and diagnostics

The parser applies configurable limits for total characters, lines, line length, blocks, fields, and nesting depth. `ParseReader` reads at most one character beyond the configured total-character budget and rejects an overlong line while scanning. Stable `SiiSyntaxErrorCode` values accompany parse exceptions, with line and column data when available. The default limits are defensive resource bounds; they have not been calibrated against genuine game saves.

The parser rejects unbalanced braces, blocks without headers, unclosed quoted values, input over configured bounds, and documents without blocks. Syntax that cannot be classified is retained and surfaced through `OpaqueLines`; a field edit is refused when such lines exist. The parser is a syntax foundation, not a complete SII grammar or game validator.

## Recovery preview boundary

`AtomicFileRecoveryPreviewer` reads the current target and recognized transaction artifacts, bounds content reads to the transaction payload limit, reports validation status and SHA-256, and can describe an exact-copy candidate targeting a separate output file. It performs no write, rename, delete, restore, or replace. Invalid or unavailable candidates have no proposed action. Validator exceptions become structured `payload_validation_failed` results; validator mutation is detected and prevents a valid recovery proposal.

The preview uses a caller-supplied validator. No game-specific validator is currently registered, and the Avalonia application does not point the previewer at production profiles. Any future recovery execution requires a separate independent architecture review and explicit operator authorization. This code does not itself implement or activate such execution.

## Verification remaining

Before claiming ETS2, ATS, mods, a save version, or Coaches DLC compatibility, add safely sourced genuine fixtures with provenance and licensing, implement the relevant container decoding and version-aware validation, and demonstrate unchanged roundtrips plus in-game or authoritative format checks. Keep profile scanning and writes disabled until those checks exist and the security gates are separately reviewed.
