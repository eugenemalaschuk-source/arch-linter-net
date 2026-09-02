## 1. Contract surface and policy validation

- [x] 1.1 Add strict/audit layout-convention applicability inventory contract models, catalog/baseline bindings, and public API review coverage; verify YAML round-trip and `make public-api-check` pass.
- [x] 1.2 Extend JSON schema, raw-YAML keys, provenance, and semantic validation for bounded scopes, normalized expected folders, unique IDs, and referenced convention IDs; verify focused policy-loading tests reject malformed or out-of-scope inventory declarations.

## 2. Applicability evaluation

- [x] 2.1 Implement deterministic bounded source-folder classification for present, stale, unmapped, and ambiguous inventory subjects; verify focused Core tests cover empty, renamed, nested, outside-scope, and overlapping-folder cases.
- [x] 2.2 Produce one canonical expected applicability entry and record per expected folder, plus an exhaustive scope entry when configured, including nonempty linked-selector evidence and strict/audit membership; verify the shared evaluator reports stable stale/unexpected-empty/unmapped/ambiguous reason codes.
- [x] 2.3 Register the inventory family through normal contract dispatch without changing layouts that do not opt in; verify the existing layout-convention suite and a compatibility regression pass.

## 3. Projection, documentation, and validation

- [x] 3.1 Verify strict and audit inventory evidence through the existing normalized Human, JSON, SARIF, Testing, and baseline seams; verify each projection retains canonical identity and provenance.
- [x] 3.2 Document audit-first and strict inventory authoring, exact bounded matching, exhaustive semantics, and non-goals; verify documentation references and examples are valid.
- [x] 3.3 Format changed files and run focused tests, affected project tests, relevant lint, and OpenSpec validation; verify all required local checks pass before archive.
