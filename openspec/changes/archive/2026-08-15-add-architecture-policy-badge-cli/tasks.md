## 1. Native CLI report paths

- [x] 1.1 Add `badge architecture-policy` with strict-validation exit semantics and Shields endpoint JSON output.
- [x] 1.2 Add `coverage report` with full/compact Markdown rendering, changed-file classification, and diff-unavailable handling.
- [x] 1.3 Add focused NUnit tests covering badge pass/fail/error and report summary, failures, changed-file, and compact-report behavior.

## 2. Repository integration

- [x] 2.1 Switch Make targets and GitHub Actions to the native CLI commands and remove the Python coverage report modules and tests.
- [x] 2.2 Update README and CI documentation with the native commands and endpoint semantics.

## 3. Verification and delivery

- [x] 3.1 Run focused CLI tests, formatter, workflow/documentation checks, and OpenSpec validation.
- [x] 3.2 Archive the OpenSpec change, amend the #594 branch, push it, and update the draft PR description.
