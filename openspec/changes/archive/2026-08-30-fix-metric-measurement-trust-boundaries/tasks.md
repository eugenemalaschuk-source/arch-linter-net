## 1. Canonical metric ownership

- [x] 1.1 Bind each metric project owner to a unique resolved assembly artifact and normalized project path; verify the Core project-owner tests pass.
- [x] 1.2 Fail closed for duplicate project artifact candidates without changing ordinary validation topology behavior; verify a same-AssemblyName regression is unassessable.

## 2. Public evidence and snapshot lifecycle

- [x] 2.1 Make unassessable Core metric contributor evidence unavailable and regenerate the approved public API surface; verify `make public-api-check` passes.
- [x] 2.2 Mark a snapshot terminally cancelled when `Measure()` observes cancellation; verify later `Measure()` and `Evaluate()` calls are rejected.

## 3. Verification and completion

- [x] 3.1 Add focused coverage for all three regressions and verify the affected Core and CLI test suites pass.
- [x] 3.2 Verify `openspec validate --all`, formatting, architecture lint, public API check, and local coverage are clean before archive.
