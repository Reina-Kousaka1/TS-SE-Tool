# ScsC Read-Only Decoder Architecture Self-Review

## Review status

Documented architecture self-review by the implementation agent; **not an
independent review**. No Architect tool is available in this session.

## Evidence

- The repository currently decodes plain-text SII bytes and raw BSII payloads
  in memory, but does not decode the outer game-save container.
- Published community [format notes](https://github.com/Istarlabs/istar-truckdata/blob/78c382a72dcae02a50cbd175a45d6230409d721a/docs/save-game-format.md)
  describe `ScsC` as an AES-256-CBC payload containing a zlib stream, with the
  inflated byte length in the header. The corresponding
  [reference reader](https://github.com/Istarlabs/istar-truckdata/blob/a5e0c39d3eaae1e1905b8fd7bea19b726af772c2/src/truckdata/savegame/savegamefile.cpp)
  supplies the shared key and explicitly does not verify the HMAC.
- No approved genuine save copy is available. Container tests must therefore
  use synthetic plaintext, compression, IV and encrypted bytes only.

## Proposed boundary

- Accept a caller-supplied byte span only; do not open, enumerate or modify
  files.
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
- Keep the component disconnected from profile discovery, editor write paths,
  recovery execution and Avalonia until real-copy validation and a separate
  security review exist.

## Risks and open questions

- Without checking the header HMAC, a crafted or damaged container may decrypt
  to attacker-controlled bytes. Bounds, zlib checks and downstream syntax
  checks limit resource and parsing risk but do not prove origin or integrity.
- The shared game key and container description come from unofficial community
  sources; there is no vendor specification or genuine fixture available here.
- Exact compatibility with current ETS2/ATS releases, Coaches saves and the
  game's HMAC verification behavior remains unverified.
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
