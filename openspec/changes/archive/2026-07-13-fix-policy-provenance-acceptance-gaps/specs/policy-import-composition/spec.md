## ADDED Requirements

### Requirement: Typed policy diagnostics reach every reporting boundary
Import and policy-validation exceptions carrying typed diagnostics SHALL retain
their policy location, related locations, and import chain through the CLI
boundary. Human output SHALL identify the policy source and explicit root;
JSON and SARIF output SHALL expose machine-readable location data rather than
only an exception string.

#### Scenario: Imported effective-schema value is invalid
- **WHEN** an imported fragment with an arbitrary filename contributes an
  effective-policy value with an invalid type or shape
- **THEN** the CLI human output identifies the fragment and root, and JSON and
  SARIF output contain the typed fragment policy location and import chain

### Requirement: Expanded layer templates retain source-template provenance
The system SHALL retain source-template typed provenance for every strict or
audit layer template expanded for execution or policy-consistency checking.
The retained provenance SHALL include source path, YAML path, family, effective
ID, and root context.

#### Scenario: Imported strict template produces a violation
- **WHEN** an imported strict layer template expands into a contract that
  produces a runtime violation
- **THEN** human, JSON, SARIF, and Testing adapter outputs identify the source
  template location from the fragment

#### Scenario: Imported audit template participates in consistency checking
- **WHEN** an imported audit layer template expands into a policy-consistency
  finding
- **THEN** the finding identifies the source template location from the
  fragment

### Requirement: Provenance location order reflects composition encounter order
Primary and related policy locations SHALL be ordered by their composed
encounter ordinal, not lexicographic YAML path. For composition conflicts, the
original declaration SHALL be primary and the conflicting declaration SHALL be
related.

#### Scenario: Double-digit contract index participates in a conflict
- **WHEN** related locations include composed nodes whose display YAML paths
  contain indices 2 and 10
- **THEN** their order follows composed encounter order, and the original
  declaration remains the primary location
