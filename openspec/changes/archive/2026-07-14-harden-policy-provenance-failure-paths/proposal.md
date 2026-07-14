## Why

PR #317 preserves provenance for ordinary imported-policy diagnostics, but three
deterministic failure paths still bypass or corrupt that provenance: malformed
root documents are parsed before a root descriptor exists, raw YAML validation
runs outside the provenance boundary, and display-style YAML paths collide when
legal mapping keys contain structural characters. These paths must produce the
same typed diagnostics and portable machine-readable output as the rest of the
policy-loading pipeline.

## What Changes

- Parse the selected root through a root source descriptor before deciding
  whether it imports fragments, so malformed, multi-document, and non-mapping
  roots expose typed source-shape diagnostics.
- Run raw layer, contextual-contract, port-boundary, and semantic-coverage
  YAML validation with the active provenance location and enrich imported
  failures before they cross the loader boundary.
- Replace concatenated display paths as provenance-map identity with escaped
  JSON Pointer keys, while retaining existing dot/index notation only for
  human-readable YAML-path display.
- Add regression coverage for JSON and SARIF policy errors, imported raw YAML
  validation, and colliding legal mapping keys.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `policy-import-composition`: require typed provenance across root parsing and
  raw validation, and require collision-safe internal provenance identity.
- `violation-reporting`: require JSON and SARIF policy-error output for the
  newly covered typed root and raw-validation failures.

## Impact

Affected code is limited to the policy loader/parser/composer/provenance index,
effective-schema lookup, and existing Core/CLI regression tests. There are no
new public DTO fields, import grammar changes, external dependencies, or
changes to rendered YAML-path notation.
