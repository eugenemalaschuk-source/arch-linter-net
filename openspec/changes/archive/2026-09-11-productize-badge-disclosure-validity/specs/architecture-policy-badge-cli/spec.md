## ADDED Requirements

### Requirement: Architecture Health badge supports closed disclosure profiles
The CLI SHALL support versioned `headline-only/v1` and
`headline-plus-freshness/v1` Architecture Health disclosure profiles. It SHALL
emit the fixed property order and default UTF-8 JSON escaping defined by the
selected profile, including only schema version, fixed label, the canonical
headline message, Health-owned color, and—only for the freshness profile—the
canonical `verified_at` and `valid_until` values. It SHALL reject unsupported
profiles or schemas and incomplete publication evidence with an actionable,
fail-closed diagnostic. Existing unprofiled Architecture Health badge output
and `badge architecture-policy` behavior SHALL remain compatible.

#### Scenario: A freshness profile preserves canonical headline semantics
- **WHEN** a user projects complete canonical Health publication evidence using
  `headline-plus-freshness/v1`
- **THEN** the CLI writes the canonical headline fields followed by only
  `verified_at` and `valid_until` in the profile's fixed representation
- **AND** its Gate, Health, counts, color, and exit code match the existing
  canonical Architecture Health projection

#### Scenario: Legacy compatibility remains available
- **WHEN** a user invokes the existing unprofiled Architecture Health badge or
  architecture-policy badge command
- **THEN** the CLI retains its established payload, color, and exit behavior
- **AND** selecting strict profile validation does not silently accept legacy
  evidence lacking required validity data

### Requirement: Disclosure schemas and fixtures are consumer-distributable
The packaged CLI/Core consumer surface SHALL include the supported disclosure
schema and canonical/adversarial fixtures required to validate supported
profiles without a source-tree project reference. The package SHALL expose a
stable documented location for those files.

#### Scenario: Freshly packed consumer validates the contract
- **WHEN** an isolated consumer installs a freshly packed product artifact
- **THEN** it can locate the disclosure schema and fixtures and run the profile
  command against canonical and rejected examples
- **AND** it does not require a project reference to the source checkout
