## 1. Canonical Core capture API

- [x] 1.1 Add public request, outcome, and stable topology-capture fact models without exposing scanning internals.
- [x] 1.2 Build one analysis session for a requested supported subject kind and reuse the normal validation observation projection for deterministic subjects and dependency witnesses.
- [x] 1.3 Expose the Core capture service through engine composition and add focused deterministic, no-declared-topology, and all-subject-kind tests.

## 2. Review-oriented CLI workflow

- [x] 2.1 Register a top-level `topology` command with capture, diff, and verify subcommands and shared strict/audit, output, build-state, and JSON error handling.
- [x] 2.2 Implement stable capture document rendering and safe output-destination guards that never replace policy inputs.
- [x] 2.3 Implement diff and verify projections from ordinary validation topology evidence, keeping structural, relational, unmapped, and stale categories distinct and preserving validation exit semantics.
- [x] 2.4 Add focused handler, command catalog, and integration tests for deterministic output, error boundaries, and strict/audit parity.

## 3. Lifecycle evidence and documentation

- [x] 3.1 Add realistic .NET server/library and Unity-style topology fixtures that cover capture, diff, and verify without policy mutation.
- [x] 3.2 Document the capture-to-review-to-declared-topology workflow, explicit non-approval boundary, and stable JSON output contract.

## 4. Approval and validation

- [x] 4.1 Update reviewed public API approval only for the intentional Core capture API surface and add approval coverage.
- [x] 4.2 Run focused Core and CLI tests, formatter, policy/OpenSpec validation, and implicated architecture/public-API checks; resolve all issue-related failures.
