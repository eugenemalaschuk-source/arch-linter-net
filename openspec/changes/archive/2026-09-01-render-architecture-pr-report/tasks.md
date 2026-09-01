## 1. Canonical Core report data and projection

- [x] 1.1 Extend the deterministic architecture-change report with ordered resolved findings and cover added/existing/resolved separation plus artifact compatibility in focused Core tests.
- [x] 1.2 Add the additive Health reporting-evidence export from one existing Health evaluation, preserving canonical inventory, lifecycle, applicability, topology, external-evidence, debt, finding, remediation, and provenance facts without re-analysis; verify it with focused Core serialization tests.
- [x] 1.3 Implement the versioned Core PR-report input reader and typed projection, including explicit unavailable evidence, non-recomputed gate/health state, stable ordering, and bounded section data; verify all required clean/debt/blocking/incomplete fixture shapes with focused Core and CLI tests.

## 2. Native CLI report surface

- [x] 2.1 Add the composed `report pr` command module, option validation, artifact I/O, output-path handling, and established failure contract; verify help and malformed/incompatible input behavior in focused CLI tests.
- [x] 2.2 Render the Core projection as deterministic architecture-only Markdown with headline, blockers, debt, completeness, change, remediation, canonical navigation, and transparent per-section truncation; verify required Markdown fixtures through CLI/unit tests.
- [x] 2.3 Exercise the installed/packed CLI `report pr` workflow with canonical local artifacts in consumer acceptance and verify it needs neither GitHub credentials nor workflow-owned report composition.

## 3. Contract, documentation, and final verification

- [x] 3.1 Update CLI/output documentation and reviewed public-API evidence for the new report contract, then verify the public API check passes.
- [x] 3.2 Run focused Core and CLI tests, `make fmt`, directly implicated lint checks, and strict OpenSpec validation; fix issue-related failures and record exact results.
- [x] 3.3 Synchronize specifications with the completed implementation, archive the OpenSpec change, run `openspec validate --all`, and inspect the resulting main specs before opening the PR.
