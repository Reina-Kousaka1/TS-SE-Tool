# ScsC Read-Only Decoder Architecture Self-Review

## Review status

Documented architecture self-review by the implementation agent; **not an
independent review**. No Architect tool is available in this session.

## Evidence

- The repository decodes plain-text SII bytes, raw BSII payloads, and the outer
  `ScsC` container in memory. One user-supplied ETS2 copy was read-only decoded
  into 19,354 BSII blocks; this confirms that sample only.
- Published community [format notes](https://github.com/Istarlabs/istar-truckdata/blob/78c382a72dcae02a50cbd175a45d6230409d721a/docs/save-game-format.md)
  describe `ScsC` as an AES-256-CBC payload containing a zlib stream, with the
  inflated byte length in the header. The corresponding
  [reference reader](https://github.com/Istarlabs/istar-truckdata/blob/a5e0c39d3eaae1e1905b8fd7bea19b726af772c2/src/truckdata/savegame/savegamefile.cpp)
  supplies the shared key and explicitly does not verify the HMAC.
- One explicitly supplied genuine ETS2 backup was inspected read-only. Its
  `ScsC` HMAC was not verified and it was not copied into the repository.
  Synthetic tests remain the only evidence for container edge cases.

## Proposed boundary

- Keep file access outside this decoder; the caller supplied a file copy for
  read-only validation, and the decoder operates on in-memory bytes.
- Decode known `ScsC` inputs in memory; pass raw `BSII` payloads to the
  bounded BSII reader and `SiiN` text to the strict text parser in a separate
  composition step. Reject unknown signatures and unsupported legacy forms.
- Bound encrypted input and announced/inflated output before allocating. Require
  AES block alignment and an exact decompressed length; reject short or
  oversized output and malformed zlib data.
- Use CBC with padding disabled because the zlib stream is followed by block
  padding bytes. Do not treat those bytes as part of the SII payload.
- Report container authentication as **unverified**. CBC is unauthenticated in
  this reader and the HMAC is not checked; decoded bytes remain untrusted.
- Keep `ScsC` reconstruction and binary editor export disabled. The real-copy
  decode does not verify HMAC, test unchanged container roundtrips, establish
  game compatibility, or satisfy an independent security review. The desktop
  may show read-only structural diagnostics without authorizing writes.

## Experimental BSII-to-text conversion

- `ExperimentalBinarySiiTextSerializer` now converts the decoder's supported
  BSII v1-v3 value types to a plain `SiiNunit` text representation in memory.
  It consumes every declared field's raw bytes, rejects invalid references,
  unresolved ordinals, unsupported string values, trailing field bytes, and
  unsupported types, then reparses the entire output and checks each block,
  field name, array count/index, and emitted value against a second bounded
  decode of the original field bytes. The verification retains only per-block
  field counts rather than a second document-sized list of expected values.
  It does not create files or serialize a ScsC container.
- The type formatting was cross-checked against public implementations and
  format notes: [avoinescu/ets2-decrypt](https://github.com/avoinescu/ets2-decrypt)
  documents a decoder tested against real 1.61/1.62 saves, and
  [scs_tools FORMAT.md](https://github.com/Wilps93/scs_tools/blob/main/FORMAT.md)
  documents the packed placement bias and SII text conventions. These are
  community sources, not an SCS specification. This repository has not yet
  compared generated output byte-for-byte or semantically against either
  implementation on the supplied sample.
- This conversion is not yet evidence of lossless game semantics. Boolean
  bytes are rendered as `true`/`false`; v2/v3 placements apply the documented
  position bias and omit its packed component in the text representation.
  Those transformations need cross-implementation and game-load validation.
  Therefore all file export and save-edit UI gates remain disabled.
- The experimental converter and its synthetic parser/format tests passed the
  seven-target GitHub Actions matrix on commit `baeac8e` (run `46135418631`).
  This verifies only the tested synthetic cases; it does not replace comparison
  against an independent decoder or an in-game load. The architecture
  self-review is not an independent review.
- The field/value reparse verification passed the .NET 10 suite on commit
  `3fadc9e` (613 tests, zero failures on Linux x64). The complete seven-job
  matrix passed after one macOS DMG creation retry; the first attempt failed
  with `hdiutil: create failed - Resource busy`. This remains syntax/output
  verification only, not semantic equivalence or game acceptance.

## Bounded fixture-inspector integration

- `SiiFixtureInspector` now invokes the existing bounded read-only decoder for
  recognized `ScsC`, `BSII`, and `3nK` inputs. It reports only aggregate format,
  version, block-count, and authentication-state metadata; it does not return
  field values or enable writes. Malformed wrappers and binary payloads remain
  explicit unsupported reports with categorized errors.
- Synthetic tests cover an encrypted ScsC-wrapped BSII document and a truncated
  ScsC prefix. CI passed on all seven targets on commit `6d65357` (run
  `46136419118`). A hash-verified copy of that build's inspector was run against
  the user-authorized `save/1` files: the 448,952-byte ScsC backup reported
  version 3 and 19,354 binary blocks after two reads with matching size and
  SHA-256. Experimental conversion of that real BSII payload also completed
  and its generated text passed the SII syntax parser. The report explicitly
  says ScsC authentication was unverified and writing disabled. The 0-byte
  primary file reported invalid syntax. Neither file was modified. This checks
  syntax coverage only; binary/text semantic equivalence and game acceptance
  remain unverified.
- A second hash-verified inspector build exercised `LegacyBinaryProfilePreview`
  on the same ScsC backup entirely in memory. Money, XP, and existing Coaches
  XP scalar changes passed exact payload undo/redo checks; six existing
  candidate Coach skill fields were found and one field passed its own exact
  undo/redo check. A final two-read source-hash comparison matched. The command
  reports no profile values or identifiers and has no file-writing path. This
  verifies editor preview behavior on this sample only, not file export or game
  acceptance.

## Risks and open questions

- Without checking the header HMAC, a crafted or damaged container may decrypt
  to attacker-controlled bytes. Bounds, zlib checks and downstream syntax
  checks limit resource and parsing risk but do not prove origin or integrity.
- The shared game key and container description come from unofficial community
  sources; there is no vendor specification and the genuine fixture is not
  checked into this repository.
- Exact compatibility with other ETS2/ATS releases, Coaches binary saves and
  the game's HMAC verification behavior remains unverified.
- No encryption, container reconstruction or save write is in scope for this
  read-only stage.

## Required validation before integration

- Synthetic AES/zlib fixtures for valid data, altered announced size, truncated
  headers, misaligned ciphertext, malformed compression, short output, excess
  output and input/output limit boundaries.
- Real, explicitly approved copies for each claimed game/version, tested
  read-only and with no source save modified.
- Independent security/architecture review before any write or editor
  integration.
