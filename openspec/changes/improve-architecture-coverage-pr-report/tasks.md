## 1. Failure-report rendering

- [x] 1.1 Collect normalized strict failure diagnostics and group them deterministically by contract.
- [x] 1.2 Render complete diagnostics in the artifact and bounded representatives in the PR comment.
- [x] 1.3 Add failed-rule and failed-diagnostic counts, coverage-summary fallback evidence, and an artifact link.

## 2. Validation

- [x] 2.1 Add script-level tests for grouping, full-detail rendering, compact truncation, fallback, and passing output.
- [x] 2.2 Run the focused report-generator tests and OpenSpec validation.

## 3. Self-policy remediation

- [x] 3.1 Repair self-policy classification overlap and stale rule-input coverage references.
- [x] 3.2 Run strict validation and remediate the resulting C# source-layout and purity violations.
- [x] 3.3 Remediate resulting linter and CI policy violations, then run focused validation.

## 4. CEL convention parity

- [x] 4.1 Classify CEL's data-only modules as Models and confirm that the package-wide abstraction and exception conventions cover CEL.
- [x] 4.2 Move CEL model declarations into local Models directories and validate strict/audit, linter, and test gates.
