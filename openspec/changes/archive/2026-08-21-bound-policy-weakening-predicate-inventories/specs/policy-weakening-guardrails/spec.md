## MODIFIED Requirements

### Requirement: Deterministic enforcement and static-scope weakening is identified

The comparator SHALL emit a `semantic` finding with stable control identity,
base/current evidence, and authored/effective provenance when same-family and
same-ID strict control is removed or downgraded to audit, a resolved
source-set member is removed, a source set or source expansion becomes
optional or empty-tolerant, a typed required rule input becomes optional, a
matched subtractive exclusion is added, a typed universal ignored violation is
added, or a supported explicit forbidden/allow-only inventory is relaxed. A
fact SHALL be compared as an inventory only when its explicitly supported typed
shape is a scalar string set of exact identities. Predicate strings and
cross-field location-union values SHALL NOT establish semantic direction from
textual set subtraction alone. Fact-name prefixes alone SHALL NOT establish
semantic direction. It SHALL retain an existing schema-backed reason as
rationale evidence when present.

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

#### Scenario: Required source expansion becomes empty-tolerant
- **WHEN** an authored source expansion retains its effective identity but
  changes from required to `optional_empty`
- **THEN** comparison reports semantic weakening with both expansion
  provenances

#### Scenario: Boolean prohibition is strengthened
- **WHEN** a known boolean prohibition changes from `false` to `true`
- **THEN** comparison does not report weakening

#### Scenario: Boolean prohibition is relaxed
- **WHEN** a known boolean prohibition changes from `true` to `false`
- **THEN** comparison reports semantic prohibition removal

#### Scenario: Base-type prefix broadens
- **WHEN** a forbidden base-type prefix changes from `UnityEngine.UI.` to
  `UnityEngine.` without a supported containment comparator
- **THEN** comparison does not report semantic prohibition removal

### Requirement: Unsupported typed-fact changes remain bounded

When a typed contract fact changes but no supported directional comparison rule
applies for that fact's actual shape, the comparator SHALL emit one
deterministic `impact_not_proven` finding with canonical base/current fact
evidence and no affected subjects. It SHALL leave facts handled by the
dedicated selector comparison to that path. Predicate and cross-field
location-union strings require a dedicated containment or trusted effective
membership comparator before semantic direction is reported.

#### Scenario: Unknown typed fact changes
- **WHEN** a same-control typed contract fact changes and has no supported
  directional weakening rule
- **THEN** comparison reports `impact_not_proven` without claiming semantic
  weakening or affected architecture subjects

#### Scenario: Structured allow-list expands
- **WHEN** a structured allow-list fact changes without a dedicated typed
  comparison rule
- **THEN** comparison reports `impact_not_proven` rather than silently
  discarding the expansion or claiming semantic direction

#### Scenario: Namespace allowance pattern changes
- **WHEN** an `allowed_only_in_namespaces` pattern changes without trusted
  effective membership evidence
- **THEN** comparison reports `impact_not_proven` rather than permission
  broadening

#### Scenario: Project allowance overlaps an allowed assembly
- **WHEN** a project allowance changes while the effective assembly membership
  of the location union is not trusted evidence
- **THEN** comparison reports `impact_not_proven` rather than permission
  broadening
