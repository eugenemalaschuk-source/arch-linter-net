## ADDED Requirements

### Requirement: UTF-8 canonical JSON stdout boundary
When `history analyze` selects `json`, the CLI SHALL emit the successful
canonical report to process standard output as UTF-8 bytes without a byte-order
mark, independently of the host console code page or `TextWriter` encoding.
Markdown and failure diagnostics remain separate text/error surfaces and SHALL
NOT alter successful JSON bytes.

#### Scenario: Non-ASCII JSON stdout
- **WHEN** successful Git evidence contains a non-ASCII canonical path or identity
- **THEN** redirected JSON stdout contains the direct UTF-8 bytes for that scalar and no BOM

### Requirement: Report serialization failure diagnostic
If successful report rendering rejects invalid internal Unicode, `history
analyze` SHALL emit a `report_serialization_invalid` diagnostic to standard
error, leave standard output empty, and exit non-zero.

#### Scenario: No report after serialization failure
- **WHEN** canonical report rendering encounters an unpaired surrogate
- **THEN** only the deterministic serialization diagnostic is emitted and no
  JSON report, Markdown report, partial ranking, or candidate set reaches stdout
