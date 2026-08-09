## 1. Non-destructive project evaluation

- [x] 1.1 Preserve existing primary outputs across the Buildalyzer project-context and framework-reference evaluation paths.
- [x] 1.2 Preserve project-aware Roslyn source/reference semantics without changing its boundary.

## 2. Regression coverage

- [x] 2.1 Add a real build-state regression that hashes selected assembly and PDB outputs before and after `--ensure-built`.
- [x] 2.2 Add CLI coverage for `--ensure-built` against a compiled fixture and verify its artifacts remain consumable.
- [x] 2.3 Add Testing API coverage for sequential `WithEnsureBuilt()` validations in one process.

## 3. Verification and specification synchronization

- [x] 3.1 Run focused build-state, CLI, and Testing API tests.
- [x] 3.2 Run formatting and full repository acceptance.
- [x] 3.3 Synchronize the implemented behavior in the OpenSpec artifacts and archive the change.
