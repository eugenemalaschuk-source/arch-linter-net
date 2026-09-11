## ADDED Requirements

### Requirement: Strict profile rejections are diagnosable and schema-bounded
The CLI SHALL accept only canonical strict-profile payloads whose numeric
headline counts are `0` or an unpadded decimal value from `1` through `9999`.
It SHALL reject any out-of-range or noncanonical count before emitting a strict
profile representation. When strict profile projection rejects an unsupported
profile, schema, legacy evidence, incomplete publication evidence, invalid
horizon, or out-of-range count, it SHALL retain the fail-closed unavailable
payload and exit code and write an actionable stable diagnostic to standard
error.

#### Scenario: A strict profile rejects noncanonical inventory counts
- **WHEN** a canonical Health report supplies a headline count with a value
  above `9999`
- **THEN** the strict profile command emits the unavailable payload and exits
  with its invalid-input status
- **AND** standard error identifies the canonical count bound

#### Scenario: A strict profile rejects legacy publication evidence
- **WHEN** a user selects a strict disclosure profile for a Health report that
  lacks the required publication-evidence receipt
- **THEN** the command emits the unavailable payload and exits fail-closed
- **AND** standard error identifies the missing, legacy, or unsupported
  publication evidence

### Requirement: Packed profile verification is platform portable
The packaged consumer regression SHALL invoke the installed global tool through
the executable name appropriate to the running operating system. It SHALL
verify both the canonical ready and canonical unavailable fixture bytes without
a source-tree project reference.

#### Scenario: A non-Windows consumer verifies shipped profiles
- **WHEN** an isolated Linux or macOS consumer installs the freshly packed CLI
- **THEN** it invokes the installed tool without a Windows-only executable
  suffix
- **AND** both canonical ready and unavailable fixtures validate successfully
