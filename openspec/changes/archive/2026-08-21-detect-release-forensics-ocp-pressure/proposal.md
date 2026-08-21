## Why

Release Architecture Forensics already produces canonical logical-file, TaskKey,
co-change, and independent-pair evidence, but it does not yet identify the
repeat-edit and role-shaped pressure that can make a file a likely Open/Closed
Principle concern. This closes the OCP analysis portion of the established v1
forensics contract without turning a heuristic into a proof.

## What Changes

- Add deterministic OCP-pressure findings for each canonical logical file.
- Derive repeated independent editing from SHA-deduplicated, pair-exclusive
  canonical TaskKey evidence.
- Add fixed ASCII filename-stem role-token evidence and the configured,
  category-local OCP score.
- Include OCP findings and their raw evidence, components, weights, aliases,
  and pathname-reuse caveat in canonical JSON output.
- Add focused tests for canonical task identity, multi-partner deduplication,
  G0-only centrality, role tokenization, and identity-boundary behavior.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `release-architecture-forensics`: Add the OCP-pressure analysis, evidence,
  score, and reporting requirements already defined for the v1 forensics
  semantic profile.

## Impact

Affected code is limited to Core history analysis and canonical reporting, plus
the Core history test suite and the existing Release Architecture Forensics
specification. No new dependencies, CLI command, or configuration shape is
introduced; the existing validated OCP weights are consumed.
