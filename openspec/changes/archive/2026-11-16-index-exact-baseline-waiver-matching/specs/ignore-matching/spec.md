# Spec Delta

## ADDED Requirements

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
