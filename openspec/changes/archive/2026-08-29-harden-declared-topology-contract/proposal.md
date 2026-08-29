## Why

Review of #508 found that the declared-topology model did not completely bind
selector matching to its observed subject kind, and that its text-composed
identity keys could collide. Those defects would force #509 to invent matching
semantics and could hide a real policy-weakening change.

## What Changes

- Define and enforce the allowed selector kinds for each topology
  `subject_kind`, including the exact owner identity semantics for namespace
  subjects.
- Replace delimiter-composed topology selector and edge identities with
  structural, collision-free values for validation, deterministic ordering, and
  weakening comparison.
- Invalidate a topology selector's cached namespace pattern when its mutable
  namespace property changes.
- Repair the topology-caused approved API, raw-validator ordering, and CLI
  policy-context schema-version test contracts; add import-composition
  provenance coverage.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `declared-topology-model`: Make selector applicability and declaration
  identity deterministic for every observed subject kind.
- `policy-context-export`: Preserve deterministic typed topology ordering and
  imported provenance without delimiter-based selector keys.
- `policy-weakening-guardrails`: Ensure distinct reviewed exclusions cannot be
  elided by colliding selector identities.

## Impact

The change affects Core topology validation, policy-context projection and
weakening comparison, schema/documentation, approved API and ordering tests,
plus focused Core and CLI regression coverage. It does not implement the #509
topology evaluator or alter the topology's public YAML shape.
