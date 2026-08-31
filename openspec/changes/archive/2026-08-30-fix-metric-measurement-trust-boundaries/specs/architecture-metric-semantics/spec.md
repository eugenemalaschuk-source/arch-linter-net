## ADDED Requirements

### Requirement: Project metric ownership binds one resolved artifact
Project topology, project footprint, and project external-dependency metric
projections SHALL bind every contributing resolved assembly artifact to exactly
one discovered project through its stable normalized project identity. They
SHALL NOT infer a project owner from an output assembly simple name. When a
contributing artifact has zero or multiple project-owner candidates, the
affected metric SHALL be unassessable with missing required input instead of
selecting a project by discovery order.

#### Scenario: Duplicate output assembly names do not merge project contributors
- **WHEN** two discovered projects have the same output assembly simple name
  and a selected metric requires ownership of an artifact with that name
- **THEN** the metric is unassessable with missing required input and does not
  publish either project as a canonical contributor
