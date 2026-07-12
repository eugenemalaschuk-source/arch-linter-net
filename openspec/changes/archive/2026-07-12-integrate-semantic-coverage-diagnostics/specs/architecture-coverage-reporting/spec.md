## ADDED Requirements

### Requirement: Semantic coverage summaries are deterministic and explainable
For every selected semantic-role coverage contract, the system SHALL emit a deterministic summary containing covered, excluded, uncovered, stale, unknown, and conflicting semantic evidence, with role, metadata, representative type, selector/consumer source, and exclusion reason where applicable.

#### Scenario: Summary separates semantic blind spots from dependency violations
- **WHEN** a semantic-role coverage contract finds an unclassified or ungoverned type and a dependency contract finds a forbidden edge
- **THEN** the semantic fact appears only in coverage output and the forbidden edge appears only in dependency output

#### Scenario: JSON and human output preserve semantic evidence buckets
- **WHEN** validation runs in JSON, CI-artifact, or human format with semantic coverage enabled
- **THEN** each format exposes the same deterministically ordered semantic summary and status-specific evidence

#### Scenario: No semantic coverage contract preserves output compatibility
- **WHEN** a policy declares no semantic-role coverage contract
- **THEN** existing coverage summaries, findings, and non-semantic output remain unchanged

### Requirement: Semantic coverage diagnostics expose conflicts and stale selectors
Semantic coverage output SHALL identify conflicting classification sources and metadata extraction failures as diagnostic evidence, and SHALL identify stale selector/contextual references by the role/metadata they reference.

#### Scenario: Conflicting classification is actionable
- **WHEN** the role index reports a conflict for a semantic fact in a semantic coverage run
- **THEN** output identifies the subject, winning/discarded roles, and source evidence without collapsing it into an uncovered dependency violation

#### Scenario: Stale selector names its unmatched semantic input
- **WHEN** a selector or contextual semantic reference matches no current fact
- **THEN** the stale evidence names its role and metadata constraints so a policy author can remove or repair it
