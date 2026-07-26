## Why

Several contract families accept exactly one source that must also appear in the analyzed target set. A rule that applies to many modules therefore has to be copy-pasted once per module, which multiplies review noise and drifts whenever a module is added or renamed.

## What Changes

- Add a schema-backed document-level `source_sets` model with a stable name, an explicit `kind` (`assembly`, `layer`, `project`), explicit members, constrained globs resolved only against declared policy inputs, and an explicit optional-empty declaration with a mandatory reason.
- Add `sources` and `source_sets` to the single-source contract families that currently cause repetition, expanding one authored contract into one deterministic contract instance per resolved source.
- Add `project_sets` and `allowed_only_in_assembly_sets` so list-shaped families reuse the same declarations without fanning out.
- Fail closed on zero-match sets, unknown set references, out-of-target sources, unusable globs, and expansions beyond a bounded instance limit.
- Record a deterministic expansion inventory that keeps the authored contract identity, the resolved source, and the exact selector, and expose it through the coverage inventory, `explain`, JSON, and SARIF.
- Update the policy JSON schema, capability manifest, authoring documentation, and AI guidance.

## Capabilities

### New Capabilities

- `source-set-expansion`: Reusable named source sets and deterministic per-source contract expansion.

### Modified Capabilities

- `architecture-coverage-inventory`: The shared coverage inventory carries the resolved source-set expansion.
- `explain-command`: `explain` reports the authored set, the concrete source, and the policy fragment for expanded contracts.
- `adoption-stabilization-compatibility`: Implements the reusable-source-set design slice.

## Impact

Affected areas include the YAML contract model and schema, policy loading and validation, policy-import provenance, contract cataloguing and execution, coverage inventory and rule-input coverage, `explain`, JSON and SARIF projections, docs, AI guidance, and NUnit tests. Existing exact-source policies are unaffected because every new key is additive and optional.
