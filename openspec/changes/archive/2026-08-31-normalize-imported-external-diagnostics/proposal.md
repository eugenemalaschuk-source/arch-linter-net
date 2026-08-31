## Why

The completed SARIF trust and selection stages stop at a typed
`SarifSelectedExternalDiagnostic`; no existing ArchLinterNet result, baseline, or output consumer
can govern that selected evidence.  This leaves valid current-context diagnostics unable to
participate in strict/audit enforcement and allows later consumers to lose the evidence context
that made them trustworthy.

## What Changes

- Add one typed imported-diagnostic detail/provenance projection that converts only trusted,
  selected SARIF diagnostics into canonical ArchLinterNet findings.
- Keep policy/evidence identity separate from persistent finding identity: stable source and
  current-context dimensions participate in the latter, while artifact hash and run facts remain
  ordered drill-down provenance.
- Reuse the normalized finding, baseline-candidate, Human, JSON, SARIF, and Testing projection
  seams; do not embed a nested source SARIF document or create an external-only result envelope.
- Project external-evidence required/optional/unassessable state through the shared applicability
  records, without revalidating the #520 trust decision.
- Document the Core consumption boundary and add focused parity tests for strict/audit,
  baseline identity, source locations, repeated runs, and wrong-context evidence.

## Capabilities

### New Capabilities

- `imported-diagnostic-normalization`: Canonical projection of selected external diagnostics and
  their source/trust provenance into governed findings and baseline candidates.

### Modified Capabilities

- `external-diagnostic-filtering`: Selected SARIF diagnostics become consumable governed findings
  while retaining the reader/selector's immutable authorization and provenance boundary.
- `diagnostics-model`: The typed diagnostic hierarchy gains an imported-diagnostic detail subtype
  and its deterministic identity/source-location semantics.
- `baseline-generation`: Exact baseline candidates can represent imported diagnostics through the
  existing reviewed lifecycle without artifact-hash or run churn.
- `multi-sink-output`: Human, JSON, and SARIF outputs preserve equivalent imported source and
  trust provenance without re-emitting the source log.
- `test-adapter`: Testing results expose imported findings through the same normalized collection
  as native findings.
- `governance-applicability-evidence`: Required external evidence is represented through the
  shared external-diagnostic applicability control rather than an imported-only completion model.

## Impact

Core model, reporting, validation result, baseline candidate, and Testing adapter APIs gain the
typed imported-diagnostic projection.  Existing SARIF reader and selector behavior remains the
sole trust/selection authority; no analyzer execution, remote service integration, or CLI artifact
location option is introduced.
