# Audited hot-path coverage and evidence (#1123)

Updated: 2026-11-15. Lifecycle role: post-v0.10.0 stabilization. This file extends the existing
#502 harness and `benchmark-evidence/v1` envelope; it is not a second benchmark authority.

## Scope and baseline

The implementation base is `a293f874` (verified `origin/main` when the issue branch was created).
The audit's historical baseline is `d74b4422c21707b1afea5e79fbdd5952597b671f` from 2026-10-31.
Historical #655 values and dispositions are retained in
[`deferred-hot-path-analysis-evidence.md`](../deferred-hot-path-analysis-evidence.md). Its graph
rows do not execute cycle or acyclic-sibling contracts; the dated interpretation correction is
appended there.

The workload manifest now has independent project, source, type, contract, ignore, metric, and
report-rule dimensions. Executable same-project type-reference graphs include dense DAG, layered
DAG, and strongly connected components with an acyclic tail. Project-reference SCCs remain
structural-only and fail closed. Workload identities are content-derived and default workloads
retain their prior identity.

## Measurement boundary

`benchmark-evidence/v1` accepts optional fixed-key work counters and timer boundary fields. Existing
artifacts remain readable and serialize without those optional properties when they are absent.
New observations do not change `analysis-profile/v1`.

Timer intervals are independent observations: fixture materialization, restore/build, process host
startup/shutdown, CLI process, Core analysis session, result publication, and total end-to-end. They
may overlap; no phase is inferred by subtracting one interval from another. If a timer, allocation,
or retained-memory measurement is unavailable, its status and reason are required. Instrumentation
overhead compares observer-disabled and observer-enabled Core analysis and also records whether
canonical results match.

Provenance fields are source revision, runtime, OS, architecture, runner identity, tool identity,
workload identity, and (when collected) a SHA-256 of materialized source/configuration inputs.
Compiled audit samples separately retain the exact DLL inventory and SHA-256 values captured from
the PE bytes loaded by Core. Public API snapshots and the analysis-profile schema are unchanged.

## Commands

Deterministic contract and schema checks:

```sh
dotnet test tests/ArchLinterNet.Core.Tests --no-restore --filter "FullyQualifiedName~BenchmarkAudit|FullyQualifiedName~BenchmarkHistoryWorkloadCatalogTests"
dotnet test tests/ArchLinterNet.Core.Tests --no-restore -c Release --filter "FullyQualifiedName~BenchmarkAuditMatrixTests" --logger "console;verbosity=normal" -- NUnit.ExplicitTests=true
```

The second command explicitly runs the environment-sensitive repeated matrix and writes one
schema-validated evidence document per #1123 Core workload. It is excluded from normal tests and
acceptance. Each workload records restore/build separately, then repeats warm observer-disabled
and observer-enabled analysis pairs in sequential and bounded Core parallel modes. The harness is
in-process, so cold process startup, CLI time, and process-attributed peak retained memory are
not measured; their evidence statuses carry reasons. No cache hit is claimed.

## Baseline, expected effect, and disposition ledger

The change is a coverage repair. It claims no product speedup. Each implementing task owns its
before/after artifact and must fill every field in this ledger; `not measured` is an explicit state,
not an inferred zero.

| Owner | Reproducer and scale | Current work baseline / phase share | Expected local and end-to-end effect | Allocation / retained-memory trade-off | Canonical result check | Post-change result / disposition |
|---|---|---|---|---|---|---|
| #1123 cycle and analysis instrumentation | `synthetic-audit-dense-dag-*`, `synthetic-audit-layered-dag-*`, and `synthetic-audit-scc-tail-*`; 4/8/12 layers, plus three sizes for each independent audit dimension | Deterministic checker and observer counters are asserted by focused tests. Direct Core phase and work counters are retained in the matrix; CLI cold-start phase share is not measured by the in-process runner. | Coverage repair only; no product speedup claimed. Success requires deterministic inputs, actual checker invocation, observer parity, and schema-valid output. Kill criterion: do not retain the observer/evidence path if it changes canonical output or the input identity is not reproducible. | Analysis allocations are retained per sample. Peak retained/process memory is unavailable with an explicit reason. | Canonical result and materialized-input identities match across paired samples; SARIF output hashes are retained for report-scale workloads. | See [issue-1123 evidence matrix](evidence/issue-1123/README.md); historical #655 data remains unchanged. |
| #1124 cycle-enumeration implementation | See [cycle-enumeration evidence](issue-1124-cycle-enumeration-evidence.md) | Existing issue artifact records direct cycle-recorder operation counts; it is not a CLI end-to-end baseline. | Optimize only the measured recorder path; end-to-end bound must use the measured phase share. | See the issue artifact; retained memory is reported separately from required output storage. | Candidate order and diagnostic identity are compared in that artifact. | Implementation evidence remains owned by #1124. |
| #1125 cycle baseline candidate | See [cycle baseline candidate evidence](issue-1125-cycle-baseline-candidate-evidence.md) | Existing issue artifact retains the current candidate baseline and deterministic graph work. | Candidate review only; no effect is attributed to #1123. | See the issue artifact. | Full candidate identity is checked in that artifact. | Implementation evidence remains owned by #1125. |
| #1138 sparse/dense line churn | Three line-count sizes; sparse and dense edit regimes | Not measured in this change; the owning task records exact LCS work and phase share. | Exact sparse-edit work reduction; dense fallback stays exact. | Allocation and peak-retained-memory values: not measured until the owning matrix runs. | Additions/deletions and canonical evidence must match the exact baseline. | Owned by #1138. |
| #1139 task-to-commit occurrences | Three task/commit sizes; sparse and co-labelled/dense occurrence regimes | Not measured in this change; count examined task-key/commit occurrences. | Remove full-history rescans from pair loops while retaining required pair output cost. | Large overlap/output allocation and retained memory are recorded separately. | Pair order, exclusive evidence, provenance, intervals, and scores must match. | Owned by #1139. |
| #1140 co-change components | Three edge counts; disconnected and dense components | Not measured in this change; count edge assignments and examinations. | Single-pass component edge assignment plus existing required sorting. | Dense component allocations and retained graph memory: not measured until the owning matrix runs. | Canonical clusters, edge membership, cohorts, and scores must match. | Owned by #1140. |
| #1141 ancestry queries | Three long-rename-chain and merged-DAG sizes, including reachable history outside the selected range | Not measured here; count distinct endpoints, visited ancestors, retained entries, and queries. | Apply the owner’s memory/headroom gate before selecting an exact strategy. | Allocation and peak retained memory are not measured until the owner runs the matrix. | Exact lineage/lifecycle decisions must match. | Owned by #1141; evidence-gated. |
| #1142 layer membership | Independent type/layer/request dimensions T/L/Q at three sizes | Historical #655 reports a small phase share for its old workload; the current phase share and membership probes are not measured here. | Cache only if the current repeated work is material and retained-memory cost fits the owner’s gate. | Cold setup and retained session state are not measured until its matrix runs. | Contextual CEL answers and ordered layer selection must match. | Owned by #1142; no change is presumed. |

## Rerun protocol

For a before/after comparison, use the same workload identity, materialized-input digest, command,
cache/prepared-state mode, parallel mode, runtime, runner, and output checksum algorithm. Retain raw
samples and analysis profiles. Use at least three sizes for each adopted family, repeat samples,
and separate cold and warm modes where the path supports both. Any material variance from a task's
expected-effect contract is explained in that task's evidence before the optimization is accepted.

No current .NET timing, allocation, peak-memory, cache-hit, or process-startup result is copied from
the source-level audit or historical #655 report.
