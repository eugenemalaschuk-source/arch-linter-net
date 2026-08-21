## ADDED Requirements

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
source-set member is removed, a matched subtractive exclusion is added, or a
supported explicit forbidden/allow-only inventory is relaxed.  It SHALL retain
an existing schema-backed reason as rationale evidence when present.

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
