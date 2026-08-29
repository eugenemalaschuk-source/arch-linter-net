## Why

Applicability membership and assessment-completion evidence now establish whether
v0.8 controls could be trusted, but that evidence is only exposed as a
completion side channel. Generic consumers cannot treat an unassessable control
as a normalized, identifiable finding across Human, JSON, SARIF, Testing, and
baseline/debt workflows.

This change supplies that shared projection before family-specific topology,
exposure, budget, and imported-diagnostic integrations land.

## What Changes

- Derive a deterministic applicability projection from the canonical expected
  membership and produced-record join; it does not parse policy YAML, recount
  effective controls, or evaluate family evidence.
- Add typed, normalized applicability diagnostics for unassessable control and
  collection-integrity evidence, retaining canonical effective-control identity,
  membership/state, reason codes, and provenance.
- Provide a deterministic control-evaluability summary with separate required,
  evaluable, unassessable, and not-applicable/optional counts for downstream
  Health and report consumers.
- Project the same evidence through existing Human, JSON, SARIF, Testing, and
  baseline-capable normalized-finding paths while preserving strict/audit
  severity and existing invalid-policy versus valid-but-unassessable boundaries.
- Document that evaluability ratios are completeness transparency only, never
  architecture-quality scoring.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `governance-applicability-evidence`: define the canonical normalized
  projection and evaluability summary over the existing applicability join.
- `diagnostics-model`: add typed applicability diagnostic details and canonical
  identity requirements to the normalized finding model.

## Impact

- Core applicability models, normalized diagnostic/finding projection, and
  formatter/SARIF integration.
- CLI validation report projections and the Testing adapter's normalized
  findings.
- Reviewed Core and Testing public API snapshots, focused unit/integration
  fixtures, and user-facing CLI documentation where output fields are exposed.
