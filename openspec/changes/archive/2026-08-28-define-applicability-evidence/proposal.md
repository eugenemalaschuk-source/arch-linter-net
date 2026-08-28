## Why

The v0.8 governance families must be able to distinguish a control that found
no violations from one that could not evaluate its intended surface. Without a
common, control-level applicability contract, topology, contract-surface,
metric, and external-evidence features would either hide incomplete evidence or
invent incompatible result envelopes.

## What Changes

- Define a shared, deterministic applicability-evidence vocabulary for v0.8
  governance controls.
- Define canonical control applicability membership, identity, state, reason,
  and provenance requirements independent of display text and finding count.
- Define family-specific evidence matrices for declared topology, contract
  surfaces, metrics/budgets, and external SARIF evidence.
- Define downstream projection boundaries so #506 and #507 can fail closed and
  normalize the evidence without duplicating coverage, policy inventory, or
  finding identity models.
- Preserve all current policy behavior until a v0.8 family explicitly opts in.

## Capabilities

### New Capabilities

- `governance-applicability-evidence`: Stable shared applicability evidence and
  control-level assessability semantics for v0.8 governance families.

### Modified Capabilities

- None.

## Impact

- Adds the OpenSpec source-of-truth contract and design artifacts used by #506,
  #507, #509, #513, #518, and #522.
- Does not add a policy schema field, production model, output format, public
  API, or behavior change in this issue.
