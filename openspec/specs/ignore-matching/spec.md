# Ignore Matching Specification

## Purpose
Matches ignored-violation entries against actual violations by exact source/forbidden-reference pairs and wildcard patterns.

## Requirements

### Requirement: Match exact source type and forbidden reference
The system SHALL match violations where both `source_type` and `forbidden_reference` exactly equal the ignore pattern values.

#### Scenario: Exact match
- **WHEN** ignore entry has `source_type: "A"` and `forbidden_reference: "B"`
- **THEN** a violation with source `"A"` and reference `"B"` is ignored

### Requirement: Match wildcard patterns
The system SHALL support `*` wildcard at the end of a pattern to match any prefix.

#### Scenario: Prefix wildcard match
- **WHEN** ignore entry has `source_type: "MyApp.*"`
- **THEN** a violation with source `"MyApp.Services.Foo"` is ignored

### Requirement: Match double-star glob patterns
The system SHALL support `**` glob patterns to match across namespace segments.

#### Scenario: Double-star cross-segment match
- **WHEN** ignore entry has `forbidden_reference: "MyApp.**.Models.*"`
- **THEN** a violation with reference `"MyApp.Domain.Models.Foo"` is ignored

### Requirement: Match single-char wildcard patterns
The system SHALL support `?` glob patterns to match exactly one character.

#### Scenario: Single-char wildcard match
- **WHEN** ignore entry has `source_type: "MyApp.?ervice*"`
- **THEN** a violation with source `"MyApp.Services.Foo"` is ignored

### Requirement: Non-matching patterns do not ignore
The system SHALL not ignore violations when the pattern does not match.

#### Scenario: No match
- **WHEN** ignore entry has `source_type: "Other"`
- **THEN** a violation with source `"MyApp.Foo"` is not ignored

### Requirement: Structured waiver matching uses canonical finding identity
The system SHALL match a complete structured waiver only when its declared
canonical lowercase fingerprint equals the versioned canonical identity assigned
to a live finding before suppression. It SHALL retain existing legacy glob
matching for entries without structured waiver fields and existing baseline
identity matching for baseline-imported entries. Source-set-expanded aliases of
one authored structured waiver SHALL contribute to that same waiver's matching
state without creating independent declarations.

#### Scenario: Similar display text does not satisfy a structured waiver
- **WHEN** two findings have the same source and forbidden-reference display
  text but different canonical assembly, member, or occurrence identity
- **THEN** a structured waiver fingerprint for one finding SHALL NOT suppress
  the other finding

#### Scenario: Matching expanded alias suppresses its exact finding
- **WHEN** a source-set-expanded alias emits the canonical finding selected by
  an authored structured waiver
- **THEN** that finding is suppressed and the authored waiver is recorded as
  matched even when another alias emits no matching finding

### Requirement: Exact ignore matching scales with indexed inputs and required matches
For a contract with I exact-path ignore entries, F finding/alias lookups, and K required match reports, exact-path work SHALL be O(I + F + K), excluding wildcard fallback. Exact equality and original ignore ordering SHALL be preserved.

#### Scenario: Tracker receives every ordered duplicate match
- **WHEN** a finding matches duplicate exact entries and overlapping wildcard entries while ignore tracking is enabled
- **THEN** every matching original index is recorded and matches are processed in original ignore order

#### Scenario: First match determines provenance without a tracker
- **WHEN** a finding matches exact and wildcard entries with different loaded-baseline provenance and ignore tracking is disabled
- **THEN** the earliest matching original entry determines loaded-baseline provenance

#### Scenario: Missing live identity retains only the legacy baseline fallback
- **WHEN** a versioned baseline entry is considered without a live identity and a structured manual waiver is incomplete or invalid
- **THEN** the versioned baseline entry uses its existing text/glob fallback and the manual structured waiver does not suppress the finding

#### Scenario: Exact work does not repeat a full scan per finding
- **WHEN** the number of exact-path ignore entries, finding lookups, and required matched entries are scaled independently for one contract
- **THEN** exact matching work grows with index preparation, findings, and required matches rather than the product of ignore entries and findings

#### Scenario: Aliases reuse one live waiver fingerprint
- **WHEN** one finding has multiple aliases and a structured waiver is applicable
- **THEN** at most one live fingerprint is built for the finding, reused for every alias, and all matching original indices are retained

#### Scenario: Invalid structured waivers remain fail-closed
- **WHEN** a structured waiver is invalid or a live identity is unavailable
- **THEN** its text/glob fields do not suppress the finding

#### Scenario: Cancellation and glob timeouts remain enforced
- **WHEN** cancellation is requested during index preparation or wildcard matching, or a glob exceeds its timeout
- **THEN** the existing cancellation or timeout behavior is preserved

#### Scenario: Wildcard work remains outside the exact-path bound
- **WHEN** a finding requires wildcard fallback
- **THEN** wildcard behavior and timeout remain unchanged and no linear runtime bound is claimed for arbitrary glob patterns
