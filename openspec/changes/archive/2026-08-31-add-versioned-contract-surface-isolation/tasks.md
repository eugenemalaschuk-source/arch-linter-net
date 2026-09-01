## 1. Policy contract and validation

- [x] 1.1 Add the strict/audit versioned-isolation policy models, contract-group aggregation, and family binding/registry entries.
- [x] 1.2 Add closed raw-YAML, schema, and typed validation for bounded local surfaces, group references, and invalid configuration.
- [x] 1.3 Register baseline, policy-context, cache, and normalized-output family plumbing while retaining the existing exposure payload and diagnostic identity.

## 2. Shared exposure evaluation and isolation behavior

- [x] 2.1 Extract the internal reusable exposure-evaluation seam without changing existing generic contract-surface exposure behavior.
- [x] 2.2 Implement deterministic version/surface-group source-root and forbidden-target resolution with fail-closed applicability evidence.
- [x] 2.3 Integrate strict/audit checker execution, ignores, findings, baseline identity, and normalized projections for the new family.

## 3. Tests and documentation

- [x] 3.1 Add focused policy/schema tests for valid authoring and invalid/unknown/duplicate/unbounded/self-referential group declarations.
- [x] 3.2 Add evaluator tests for direct and nested cross-version leaks, internal target leakage, same-name identity, strict/audit output, baseline identity, and zero-match applicability.
- [x] 3.3 Document static versioned contract-surface isolation syntax, diagnostics, applicability, and runtime-compatibility non-goals.

## 4. Verification and specification lifecycle

- [x] 4.1 Run focused Core tests, formatter, affected policy/lint checks, and OpenSpec validation; fix issue-related failures.
- [x] 4.2 Synchronize the proposal/design/spec/tasks with the delivered behavior and archive the OpenSpec change.
