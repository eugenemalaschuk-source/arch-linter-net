# Tasks

## 1. Capture the current baseline

- [x] 1.1 Add an explicit issue-specific harness over the existing #502 History workload catalog and record the pre-change source identity, deterministic reduced DP-cell counts, exact outputs, direct timing/allocation samples, and representative integrated ingestion share in `docs/internal/large-solution-benchmarking/issue-1138/`; verify the report includes reproducible commands and marks unavailable memory values as not measured.

## 2. Implement and prove the bounded exact path

- [x] 2.1 Add the bounded Myers distance search with measured `K = 32`, exact LCS-length conversion, and the existing two-row fallback; verify the focused `HistoryLineChurnTests` suite preserves current byte, line, binary, and rename-routing behavior.
- [x] 2.2 Add exhaustive small-sequence oracle parity and deterministic sparse-work/fallback assertions covering repeated lines, ambiguous alignments, distant edits, reversed/disjoint data, empty input, line endings, and NUL routing; verify all new cases pass in the focused History test project.

## 3. Measure impact and close the spec change

- [x] 3.1 Rerun the same #502 sparse/dense matrix after implementation and record result digests, deterministic work, median elapsed time, allocations, storage bounds, and integrated phase-share/Amdahl bound in the issue-1138 evidence; verify baseline and current results use identical catalog inputs and environment labels.
- [x] 3.2 Confirm 10k sparse work is ≤5% of baseline DP cells, compare environment-labelled direct and full-ingestion timings as informational evidence, explain sample variance and unmeasured peak memory, synchronize/archive the OpenSpec change, and verify `openspec validate --all` passes.
- [x] 3.3 Run focused tests, format changed C# files with the repository formatter, and run directly implicated architecture/public-API checks; verify the final diff contains only issue-related files and exact validation results are recorded for the PR.

## Workflow follow-up

- Open one pull request targeting `main` after all implementation, evidence, and spec synchronization tasks are complete.
