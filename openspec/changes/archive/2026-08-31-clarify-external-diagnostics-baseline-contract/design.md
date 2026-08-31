## Context

`ArchitectureImportedDiagnosticBaselineProjector` maps a selected diagnostic into the established
`ArchitectureBaselineCandidate` shape. The candidate preserves its canonical identity and
strict/audit contract group for debt matching; typed diagnostic provenance remains on the finding
and reporting projections instead.

## Goals / Non-Goals

**Goals:**

- State the actual baseline retention boundary in the federation contract and guide.
- Preserve the explicit full-provenance guarantee for canonical finding, Human, JSON, SARIF, and
  Testing outputs.
- Publish the correction through a standard archive without changing runtime behavior.

**Non-Goals:**

- Extend baseline candidates with run ID, artifact path, artifact hash, or source diagnostics.
- Change baseline comparison, debt lifecycle, identity, or diagnostic projection behavior.

## Decisions

- Describe baseline candidates as stable canonical-identity and strict/audit debt-lifecycle input.
  This matches the established `ArchitectureBaselineCandidate` model and prevents artifact/run
  churn from becoming debt churn.
- Leave the prior hardening archive immutable and archive this corrective delta separately. The
  new archive is the authoritative historical explanation for the current-spec correction.

## Risks / Trade-offs

- [A broad provenance claim could reappear in another projection list] → list baseline separately
  from the full-provenance consumers in both the requirement and public guide.
