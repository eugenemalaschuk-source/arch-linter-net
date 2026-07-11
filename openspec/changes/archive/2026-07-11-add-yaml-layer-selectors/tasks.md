## 1. Policy model and validation

- [x] 1.1 Add typed layer selector and metadata model binding, including selector-only layer support.
- [x] 1.2 Update policy schema and document validation for selector shape, namespace-or-selector requirements, and deterministic invalid-selector diagnostics.

## 2. Runtime matching and contract integration

- [x] 2.1 Implement session-aware layer type resolution using namespace predicates and the per-run role index with exact AND-combined selector matching.
- [x] 2.2 Route existing dependency, layer, allow-only, cycle, independence, protected, and external-source layer paths through selector-aware resolution while preserving namespace/glob/suffix/external behavior.
- [x] 2.3 Update layer descriptions, empty-layer/configuration diagnostics, and selector match provenance for selector-backed layers.

## 3. Tests and documentation

- [x] 3.1 Add model/schema and invalid/empty selector tests, including selector-only, namespace+selector, role-only, metadata, and invalid selector cases.
- [x] 3.2 Add runtime regression tests for namespace behavior and selector-backed layer resolution through representative existing contract paths.
- [x] 3.3 Update YAML schema reference, semantic classification docs, layers documentation, and examples.

## 4. Verification and spec lifecycle

- [x] 4.1 Run formatting and repository acceptance validation; fix all failures.
- [x] 4.2 Synchronize the main semantic-classification specification and archive the completed OpenSpec change.
