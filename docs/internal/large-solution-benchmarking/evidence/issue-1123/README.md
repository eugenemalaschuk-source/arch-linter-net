# Issue #1123 audited hot-path baseline

Captured on 2026-11-16 from source revision `b39285657fca545af7706206a6e4cb0c45da8dc7`.
The run used .NET 10.0.14 on macOS 15.8.1 (x64), runner label `local-x64-6cpu`, and
`ArchLinterNet.Core.Tests/0.1.0.0`. Each linked file is a `benchmark-evidence-archive/v1` envelope;
its gzip/base64 chunks restore the complete `benchmark-evidence/v1` JSON payload, including all raw
`analysis-profile/v1` samples. The payload SHA-256 is checked on restore, and the matrix validates
both schemas before writing each archive. Files stay below 96 KiB, under 300 lines, and with lines
shorter than 8 KiB so GitHub returns complete review patches. See the [archive schema](../../benchmark-evidence-archive-v1.schema.json)
and [evidence schema](../../benchmark-evidence-v1.schema.json).

## Matrix

The matrix has 33 independently materialized workloads: eight audit dimensions and three
executable contract-layer graph shapes, each at three sizes. Every workload has eight retained warm
Core samples: four sequential and four bounded (`maxParallelism=6`). Each mode contains two
observer-disabled/enabled pairs, with pair order alternated. One additional warmup pair is discarded.
The harness checks input, loaded-DLL, and canonical-result parity in every pair; report-scale
workloads also check stable SARIF output hashes. Evidence files retain raw analysis profiles, fixed
work counters, observer overhead, allocation deltas, and phase boundaries. Metric workloads call `Measure()` and
retain each complete metric result plus a canonical result digest in every sample. Method-body rule
counts are reported independently from ordinary dependency-contract counts. Each report-rule
workload emits exactly 32 SARIF findings at all three rule scales, with every declared rule
referenced.

| Family | Scale values | Evidence files (small / medium / large) |
|---|---:|---|
| Projects | 2 / 8 / 24 projects | [small](b39285657fca/synthetic-audit-projects-small.json) · [medium](b39285657fca/synthetic-audit-projects-medium.json) · [large](b39285657fca/synthetic-audit-projects-large.json) |
| Source files | 1 / 8 / 24 files per project | [small](b39285657fca/synthetic-audit-files-small.json) · [medium](b39285657fca/synthetic-audit-files-medium.json) · [large](b39285657fca/synthetic-audit-files-large.json) |
| Contracts | 1 / 8 / 32 contracts | [small](b39285657fca/synthetic-audit-contracts-small.json) · [medium](b39285657fca/synthetic-audit-contracts-medium.json) · [large](b39285657fca/synthetic-audit-contracts-large.json) |
| Ignore rules | 1 / 16 / 64 rules | [small](b39285657fca/synthetic-audit-ignores-small.json) · [medium](b39285657fca/synthetic-audit-ignores-medium.json) · [large](b39285657fca/synthetic-audit-ignores-large.json) |
| Finding candidates | 1 / 16 / 64 findings | [small](b39285657fca/synthetic-audit-findings-small.json) · [medium](b39285657fca/synthetic-audit-findings-medium.json) · [large](b39285657fca/synthetic-audit-findings-large.json) |
| Metric rules | 1 / 8 / 32 rules | [small](b39285657fca/synthetic-audit-metrics-small.json) · [medium](b39285657fca/synthetic-audit-metrics-medium.json) · [large](b39285657fca/synthetic-audit-metrics-large.json) |
| Report rules | 1 / 8 / 32 rules | [small](b39285657fca/synthetic-audit-reports-small.json) · [medium](b39285657fca/synthetic-audit-reports-medium.json) · [large](b39285657fca/synthetic-audit-reports-large.json) |
| Method-body rules | 1 / 4 / 16 rules | [small](b39285657fca/synthetic-audit-method-bodies-small.json) · [medium](b39285657fca/synthetic-audit-method-bodies-medium.json) · [large](b39285657fca/synthetic-audit-method-bodies-large.json) |
| Dense DAG | 4 / 8 / 12 layers | [small](b39285657fca/synthetic-audit-dense-dag-small.json) · [medium](b39285657fca/synthetic-audit-dense-dag-medium.json) · [large](b39285657fca/synthetic-audit-dense-dag-large.json) |
| Layered DAG | 4 / 8 / 12 layers | [small](b39285657fca/synthetic-audit-layered-dag-small.json) · [medium](b39285657fca/synthetic-audit-layered-dag-medium.json) · [large](b39285657fca/synthetic-audit-layered-dag-large.json) |
| SCC with acyclic tail | 4 / 8 / 12 layers | [small](b39285657fca/synthetic-audit-scc-tail-small.json) · [medium](b39285657fca/synthetic-audit-scc-tail-medium.json) · [large](b39285657fca/synthetic-audit-scc-tail-large.json) |

This run supersedes the earlier matrices in `0356a573b27b/`, `0d089308ccec/`,
`4a5c95c8f750/`, `e2695484fc06/`, `18f7c65dfc4b/`, and `ebe7e5d9eafa/`. Their generated
directories were removed from the current tree; prior source-revision snapshots remain available
in Git history.

The canonical dimensions and selection rules remain in the
[`workload-catalog.json`](../../workload-catalog.json). Project-reference SCCs remain structural-only
and are not used for these buildable contract-layer graph cases.

## Measurement limits

This is a direct Core runner matrix. Fixture materialization and restore/build are separately timed;
the per-sample Core interval includes policy loading, runner setup, assembly resolution, and contract
evaluation. Report-rule workloads separately time SARIF publication. The harness does not launch
the CLI or a child analyzer process, so cold process startup, CLI timing, and process-attributed peak
working set are unavailable or not applicable with reasons in each evidence document. Per-sample
allocation is the test-process GC allocation delta around the Core interval; peak retained managed
memory is unavailable. Cache mode is disabled and no cache-hit work is claimed.

These artifacts establish reproducible workload and work-count coverage. They do not claim a
product speedup. The shared History workload constructors have deterministic input and expected
shape-summary digests, but do not execute the owner algorithms; production output baselines and
algorithm counters remain with #1138–#1141.

## Reproduction

From a clean checkout at the recorded source revision, with restore assets present:

```sh
dotnet test tests/ArchLinterNet.Core.Tests --no-restore -c Release --filter 'FullyQualifiedName~BenchmarkAuditMatrixTests' --logger 'console;verbosity=normal' -- NUnit.ExplicitTests=true
```

The explicitly selected test writes one evidence document per workload under this directory, in a
subdirectory named for the full source revision's first 12 characters. The matrix is excluded from
ordinary tests and acceptance runs.
