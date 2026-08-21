## 1. Canonical graph projection

- [x] 1.1 Add immutable retained-vertex, pair-evidence, cohort, and cluster models that preserve canonical file, TaskKey, and rename provenance.
- [x] 1.2 Build `G0` from canonical file-event commits and canonical TaskKey file episodes; normalize base edges per endpoint-category cohort and construct threshold-only clusters.
- [x] 1.3 Wire the effective policy configuration and resulting graph into successful history ingestion without changing the fail-closed boundary.

## 2. Conformance tests

- [x] 2.1 Add NUnit fixtures for TaskKey identity/namespace behavior, task-only associations, commit-only topology, and delete/re-add/ambiguous-rename identity preservation.
- [x] 2.2 Add fixtures for cohort-local normalization, input-order independence, inclusive thresholds, non-rescoring threshold behavior, and qualifying-edge cluster aggregation.

## 3. Validation and synchronization

- [x] 3.1 Run focused Core tests, formatting, implicated lint/spec checks, and inspect the final diff.
- [x] 3.2 Synchronize the proposal/spec with the implemented behavior and archive the OpenSpec change.
