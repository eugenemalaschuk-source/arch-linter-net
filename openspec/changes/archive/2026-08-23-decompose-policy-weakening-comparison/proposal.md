## Why

Policy-weakening comparison has accumulated six independent rule families in
one comparer, while the comparer also reaches into the presentation formatter
to validate contexts and resolve membership evidence. That makes an internal
rule change unnecessarily cross-cutting and reverses the intended direction
from semantic comparison to output projection.

## What Changes

- Retain `ArchitecturePolicyWeakeningComparer.Compare(...)` as the supported
  public façade and its one deterministic finding aggregation point.
- Move each existing rule family into a focused internal evaluator without
  changing weakening identities, classifications, severities, evidence,
  provenance, rationale, de-duplication, or ordering.
- Move context compatibility validation, membership-evidence resolution, and
  the canonical context digest behind a comparison/shared-support seam; retain
  the formatter's public digest API as a compatibility delegation point.
- Add focused family and cross-family regression coverage, including output
  projection parity through the existing normalized result contract.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `policy-weakening-guardrails`: Preserve the existing comparison behavior
  while making the stable public façade, independent rule-family evaluation,
  deterministic aggregation, and formatter-free comparison-support boundary
  explicit architectural requirements.

## Impact

The change affects only internal implementation boundaries under
`ArchLinterNet.Core.PolicyWeakening` and focused Core tests. It introduces no
public API, schema, policy-context, YAML, live-repository, or output-format
change.
