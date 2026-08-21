## MODIFIED Requirements

### Requirement: Deterministic enforcement and static-scope weakening is identified

The comparator SHALL emit a `semantic` finding with stable control identity,
base/current evidence, and authored/effective provenance when same-family and
same-ID strict control is removed or downgraded to audit, a resolved
source-set member is removed, a source set or typed required rule input becomes
optional, a matched subtractive exclusion is added, a typed universal ignored
violation is added, or a supported explicit forbidden/allow-only inventory is
relaxed. It SHALL retain an existing schema-backed reason as rationale evidence
when present.

#### Scenario: Strict control becomes audit
- **WHEN** a strict contract and an audit contract share the same family and
  reviewed effective ID across base and current context
- **THEN** comparison reports a semantic strict-to-audit weakening

#### Scenario: Imported control disappears
- **WHEN** an effective strict contract from an imported source is absent from
  the current context
- **THEN** comparison reports semantic control removal with the imported
  authored provenance

#### Scenario: Explicit subtraction widens governed scope exclusion
- **WHEN** a current source expansion has a newly matched source or source-set
  exclusion
- **THEN** comparison reports semantic static-scope weakening

#### Scenario: Required source set becomes optional
- **WHEN** a named source set retains its resolved members but changes from
  required to optional
- **THEN** comparison reports semantic weakening with both source-set
  provenances

#### Scenario: Universal ignore uses typed matchers
- **WHEN** a current ignored violation has typed `source_type` and
  `forbidden_reference` matchers that are both `*`
- **THEN** comparison reports semantic universal-exception weakening without
  parsing its display detail

## ADDED Requirements

### Requirement: Project-discovery glob changes remain bounded

The comparator SHALL treat `analysis.project_include` and
`analysis.project_exclude` as project-discovery glob predicates, not literal
inventories. Without complete resolved project membership evidence, a changed
glob SHALL produce deterministic `impact_not_proven` evidence and SHALL NOT be
classified as semantic scope weakening.

#### Scenario: Include glob broadens textually
- **WHEN** `project_include` changes from `src/Core/**` to `src/**` without
  resolved project membership evidence
- **THEN** comparison reports `impact_not_proven` rather than semantic
  weakening

#### Scenario: Exclude glob narrows textually
- **WHEN** `project_exclude` changes from `tests/**` to `tests/Fixtures/**`
  without resolved project membership evidence
- **THEN** comparison reports `impact_not_proven` rather than semantic
  weakening
