## MODIFIED Requirements

### Requirement: Project metric ownership binds one resolved artifact
Project topology, project footprint, and project external-dependency metric
projections SHALL bind every contributing resolved assembly artifact to exactly
one discovered project through its stable normalized project identity. They
SHALL NOT infer a project owner from an output assembly simple name. A metric
project-topology projection SHALL retain that artifact-derived identity when it
classifies subjects and relations, while accepting the existing project selector
spelling only as a policy-facing display selector. When a contributing artifact
has zero or multiple project-owner candidates, or when a selected legacy
project selector corresponds to more than one artifact-derived project subject,
the affected metric SHALL be unassessable with missing required input instead
of selecting a project by discovery order or merging the subjects.

#### Scenario: Duplicate output assembly names do not merge project contributors
- **WHEN** two discovered projects have the same output assembly simple name
  and a selected metric requires ownership of an artifact with that name
- **THEN** the metric is unassessable with missing required input and does not
  publish either project as a canonical contributor

#### Scenario: Project relation retains exact artifact ownership before mapping
- **WHEN** two resolved artifacts share an output assembly simple name, their
  discovered project bindings are distinct, and an observed direct edge starts
  from one artifact
- **THEN** the metric projection retains the starting artifact's normalized
  project identity until it has established that the selected project selector
  denotes exactly one projected project subject
