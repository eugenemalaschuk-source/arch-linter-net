## ADDED Requirements

### Requirement: Authored analysis-scope changes remain bounded

The comparator SHALL treat `analysis.target_assemblies`, `analysis.projects`,
and `analysis.source_roots` in a policy-context artifact as authored inputs,
not proof of the effective analysed inventory. Without trusted resolved
discovery and scanner-scope evidence, a changed value SHALL produce
deterministic `impact_not_proven` evidence and SHALL NOT be classified as
semantic scope weakening.

#### Scenario: Source root broadens textually
- **WHEN** `source_roots` changes from `src/Core` to `src` without a
  path-containment/effective-root comparator
- **THEN** comparison reports `impact_not_proven` rather than semantic scope
  weakening

#### Scenario: Source roots become empty
- **WHEN** `source_roots` changes from `src` to an empty authored list without
  trusted effective scanner-root evidence
- **THEN** comparison reports `impact_not_proven` rather than semantic scope
  weakening

#### Scenario: Target assemblies become discovery-backed
- **WHEN** an authored target-assembly list changes to empty and the artifact
  does not prove whether project discovery supplies effective assemblies
- **THEN** comparison reports `impact_not_proven` rather than semantic scope
  weakening
