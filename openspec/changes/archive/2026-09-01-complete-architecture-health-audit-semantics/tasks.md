## 1. Severity-aware evidence projection

- [x] 1.1 Project coverage receipts using `CoverageConfig`, retaining warning findings as non-blocking evidence and error findings as failures.
- [x] 1.2 Add a deterministic `audit_evidence` dimension for audit validation findings, cycles, and imported diagnostics without altering strict gate semantics.
- [x] 1.3 Add projector regressions for warning/error coverage, audit-only diagnostics, equal codes with distinct provenance, and each waiver lifecycle state.

## 2. One-snapshot proof and contract updates

- [x] 2.1 Expose Health snapshot counters additively and assert one composition, project graph evaluation, snapshot, and assembly load alongside candidate-receipt reuse.
- [x] 2.2 Update user documentation and reviewed public API snapshots for the Health result and new dimension semantics.

## 3. Validation and lifecycle

- [x] 3.1 Run focused Core/CLI/Testing tests, formatter, public API check, strict architecture and docs checks.
- [x] 3.2 Run full acceptance, archive the OpenSpec change, and validate all specs.
