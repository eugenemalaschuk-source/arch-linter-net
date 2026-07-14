## Why

Imported policy fragments currently compose into one effective YAML document, but the
composition step discards which source file and YAML path owned each resolved node.
That makes downstream configuration and contract diagnostics point at the root or omit
policy origin entirely, defeating the usability and reproducibility benefits of policy
decomposition required by issue #282.

## What Changes

- Preserve typed root/fragment source descriptors and per-node YAML-path provenance
  through composition, deserialization, fallback ID assignment, and validation.
- Attach graph-derived document roles, repository-relative source paths, contract
  family/ID context, source order, and import chains where applicable.
- Report both declarations for map, singleton, and contract-ID conflicts and retain the
  owning fragment for shape, schema, semantic, consistency, and configuration findings.
- Add policy-origin metadata to human diagnostics and as additive fields in CI JSON and
  SARIF results without changing existing diagnostic categories or source-code locations.
- Cover root-inline, fragment, nested-import, arbitrary-filename, rename-invariance,
  cross-platform path, and monolithic-policy compatibility cases.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `policy-import-composition`: Specify the typed resolved-node provenance model and its
  use by import, schema, semantic, consistency, and configuration diagnostics.
- `diagnostics-model`: Allow every typed diagnostic to carry additive policy-origin and
  related-origin metadata rendered consistently in human and CI JSON output.
- `sarif-diagnostics-output`: Represent policy definition origins as stable SARIF
  related locations without replacing existing physical or logical code locations.

## Impact

The change affects the policy import resolver/parser/composer, the loaded policy model,
validator error mapping, validation diagnostics, and the shared human/JSON/SARIF
formatters. CLI, Testing, graph, and explain continue to accept one policy path and use
the existing loader/application-service APIs. No new dependency or breaking policy
format change is introduced.
