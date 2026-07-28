## ADDED Requirements

### Requirement: Typed policy-check diagnostics and provenance
Policy-check failures SHALL use typed configuration diagnostics. A selected root policy SHALL be identified as a root; imported-fragment diagnostics SHALL include authored location and the full import chain.

#### Scenario: Fragment diagnostic is rendered
- **WHEN** an imported fragment contains an invalid declaration
- **THEN** each output projection preserves its typed category, authored location, and full import chain

### Requirement: Typed deferred policy checks
The normalized diagnostics model SHALL represent deferred policy checks with a stable kind and reason that identifies the unavailable assembly, project, or source fact.

#### Scenario: Fact-dependent selector is deferred
- **WHEN** policy check encounters a syntactically valid selector requiring assembly facts
- **THEN** machine-readable output contains a typed deferred record instead of a failure or clean architecture finding
