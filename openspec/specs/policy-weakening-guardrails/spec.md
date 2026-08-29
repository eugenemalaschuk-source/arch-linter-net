# policy-weakening-guardrails Specification

## Purpose
TBD - created by archiving change add-policy-weakening-guardrails. Update Purpose after archive.
## Requirements
### Requirement: Separately authoritative policy contexts are compared fail-closed

Core SHALL compare a base and current `architecture-policy-context` artifact
only after validating supported schema/kind, required policy identity,
contracts, source-set and provenance collections, and compatible policy
identity.  It SHALL not load a base policy from the current working tree, and
it SHALL reject incomplete or incompatible input rather than return no
weakening.

#### Scenario: Identical effective policy contexts are a no-op
- **WHEN** base and current contexts have equal effective typed semantics but
  differ only in authored formatting or ordering
- **THEN** comparison returns a deterministically ordered empty finding list

#### Scenario: Context input is incomplete
- **WHEN** either context lacks a required identity or effective-policy
  collection
- **THEN** comparison fails with an actionable input error

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

### Requirement: Unproved selector impact remains bounded

The comparator SHALL not infer selector inclusion ordering or affected subjects
from raw selector text, validation pass state, or architecture-change snapshots.
It SHALL report a deterministic `impact_not_proven` finding for changed
fact-dependent selector/public-surface or bounded broad-exception shapes, with
no affected subjects unless matching complete trusted membership evidence is
supplied for both contexts.

#### Scenario: Selector change has no membership evidence
- **WHEN** a paired control changes a role, type, attribute, inheritance, CEL,
  or public-surface selector and no complete matching membership evidence exists
- **THEN** the result is an `impact_not_proven` finding with no fabricated
  affected subject

#### Scenario: Selector change has matching membership evidence
- **WHEN** complete supported base and current membership evidence is bound to
  both contexts and proves subjects were removed from the same control
- **THEN** the finding includes only those canonical affected subject identities

### Requirement: Normalized output and severity preserve guardrail semantics

The comparison result SHALL contain one normalized, deterministic finding model
for human, JSON, and SARIF output.  Each finding SHALL state stable weakening
kind/control identity, classification, configured `error`/`warn`/`off`
severity, base/current evidence, provenance, and rationale where available.
Policy weakening findings SHALL remain change-time evidence and SHALL not be
assigned baseline-debt lifecycle identity.

#### Scenario: Output formats agree
- **WHEN** a comparison produces a semantic and an impact-not-proven finding
- **THEN** Human, JSON, and SARIF expose the same identities, classifications,
  severity, and evidence

#### Scenario: Warning policy does not become baseline debt
- **WHEN** current policy configures weakening severity as `warn`
- **THEN** the finding remains visible without a failing guardrail outcome or
  a persistent architecture baseline-debt identity

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

### Requirement: Unsupported typed-fact changes remain bounded

The comparator SHALL keep unsupported typed-fact changes within a deterministic bounded outcome.

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

### Requirement: Focused comparison boundaries preserve normalized guardrail semantics

`ArchitecturePolicyWeakeningComparer.Compare(...)` SHALL remain the stable
public comparison façade and the sole deterministic aggregation point. It
SHALL orchestrate focused internal boundaries for enforcement, authored
analysis scope, static/source scope, contract facts and optionality,
exceptions, and selector/membership evidence. Comparison validation,
membership-evidence resolution, and canonical context-digest calculation SHALL
be owned by comparison/shared support rather than formatter-facing internals.
Human, JSON, and SARIF formatting SHALL remain projections of the normalized
comparison result; evaluation SHALL not depend on a formatter. Evaluators SHALL
not load YAML, inspect live repository state, or reanalyse a candidate policy.

#### Scenario: Cross-family comparison remains normalized and deterministic
- **WHEN** independently changed policy contexts produce findings from more
  than one comparison family
- **THEN** the public façade returns the same de-duplicated, ordinally ordered
  normalized findings and all output projections preserve their established
  identities and evidence

### Requirement: Structured waiver changes remain normalized weakening evidence
The policy-weakening comparer SHALL recognize added structured waivers, changed
exact targets, and extensions of an existing waiver's expiry from typed
policy-context waiver evidence. It SHALL emit existing normalized change-time
findings with the configured policy-weakening severity and provenance, and
SHALL NOT create baseline debt or independently compose a gate result.

#### Scenario: New structured waiver is visible to the existing guardrail
- **WHEN** current context adds a complete structured waiver absent from base
  context under the strict v0.8 profile
- **THEN** comparison emits a deterministic configured-severity weakening
  finding identifying the waiver ID, governed contract, target, and provenance

#### Scenario: Extending a structured waiver expiry is visible to the existing guardrail
- **WHEN** current context retains a strict v0.8 structured waiver's ID,
  contract, and exact target but moves its expiry to a later date
- **THEN** comparison emits a deterministic configured-severity semantic
  weakening finding identifying the waiver ID, target, previous expiry, and
  current expiry

### Requirement: Reviewed topology scope broadening remains visible to generic weakening comparison
The existing policy-weakening comparison SHALL consume typed topology context
facts and expose a normalized finding when current policy adds a reviewed
topology out-of-scope declaration or makes a same-identity exclusion broader
where direction is statically decidable. It SHALL use existing generic
comparison/provenance/formatter semantics, not a topology-specific weakening
engine; a selector change whose containment cannot be proven SHALL retain
bounded impact-not-proven semantics.

#### Scenario: New topology exclusion is visible
- **WHEN** current policy adds a reasoned reviewed out-of-scope topology declaration absent from base policy
- **THEN** comparison emits deterministic weakening evidence with base/current typed provenance
