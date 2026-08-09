## 1. Preserve consumable project-reference paths

- [x] 1.1 Replace full intermediate-output redirection with a unique design-time `CleanFile` name.
- [x] 1.2 Remove only generated clean manifests after Buildalyzer returns.
- [x] 1.3 Apply the non-destructive clean isolation to Roslyn and FrameworkReference evaluation.

## 2. Regression coverage

- [x] 2.1 Assert returned project-reference paths exist after `Resolve()` returns.
- [x] 2.2 Run end-to-end project-aware method-body analysis against a project reference.

## 3. Verification and synchronization

- [x] 3.1 Run focused resolver, project-aware consumer, and `--ensure-built` regressions.
- [x] 3.2 Run formatting and full repository acceptance.
- [x] 3.3 Archive the change and validate all OpenSpec specifications.
