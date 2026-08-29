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
