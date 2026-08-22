## ADDED Requirements

### Requirement: Scalar-valid canonical report serialization
Every property name and string value in a successful canonical JSON report
SHALL contain only Unicode scalar values. A valid UTF-16 surrogate pair SHALL
be retained as its corresponding non-BMP scalar without normalization. A lone
high or lone low surrogate SHALL cause canonical report serialization to fail
before any successful report text or bytes are returned; it SHALL NOT be
replaced, escaped as a surrogate code unit, or silently omitted.

#### Scenario: Valid non-BMP scalar
- **WHEN** finalized report evidence contains a valid non-BMP Unicode scalar
- **THEN** the canonical JSON report retains that scalar as direct UTF-8 content

#### Scenario: Invalid internal Unicode
- **WHEN** optional enrichment context contains a lone high or lone low UTF-16 surrogate
- **THEN** successful report serialization fails deterministically without
  producing a partial report or candidate set

### Requirement: Byte-level canonical report regression vectors
The report implementation SHALL retain focused byte-level regression coverage
for UTF-8-without-BOM encoding, LF framing, non-ASCII and non-BMP scalars,
scalar-value ordering, every enrichment status, candidate/source ordering, and
separation of successful report output from diagnostics. These vectors SHALL
assert exact bytes or deterministic rejection rather than only JSON substrings.

#### Scenario: Contract-vector repeat
- **WHEN** one byte-level report vector is rendered repeatedly
- **THEN** the encoded UTF-8 report bytes are exactly identical and contain no BOM
