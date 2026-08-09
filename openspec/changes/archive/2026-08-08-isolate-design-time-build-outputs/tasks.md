## 1. Isolate design-time evaluation

- [x] 1.1 Use a unique temporary `IntermediateOutputPath` for project-aware Roslyn resolution.
- [x] 1.2 Use the same isolation for FrameworkReference evaluation.
- [x] 1.3 Remove the broad post-hoc output snapshot and restore mechanism.

## 2. Regression coverage

- [x] 2.1 Add a concurrent reader that detects transient missing or changed selected primary outputs.
- [x] 2.2 Prove that an unrelated output changed during evaluation is not restored.

## 3. Verification and specification synchronization

- [x] 3.1 Run focused resolver and `--ensure-built` API/CLI regressions.
- [x] 3.2 Run formatting and full repository acceptance.
- [x] 3.3 Archive the corrective OpenSpec change and validate all specifications.
