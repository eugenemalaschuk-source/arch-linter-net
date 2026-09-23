# Issue #990 repository-metrics projection evidence

## Scope

This is the issue-specific v0.9 scale evidence for the repository-metrics
projection. It reuses the canonical #502 large-solution corpus and compares the
pre-fix PR head with the repaired head on two fresh, equivalent fixtures. The
measurement is not a universal hardware-independent SLA; deterministic work
counters are the primary acceptance evidence and timings are supporting
observations.

## Workload and environment

The run used the catalog workload `synthetic-very-large-multi-project` from
`large-solution-workload/v1`, with the `real_ms_build` consumer mode and the
declared `full_governance` command mix. The fixture was generated twice from the
same manifest: once for the baseline CLI and once for the repaired CLI. The
workload is synthetic and contains no private adopter identity or topology.

| Dimension | Value |
|---|---:|
| Projects / assemblies | 32 / 32 |
| Types / public types | 512 / 512 |
| Source files / source roots | 1,024 / 4 |
| Dependency edges | 496 |
| Layers / selector terms | 8 / 16 |
| Contracts / finding candidates | 8 / 128 |
| Graph shape | dense, acyclic, max depth 31 |

Environment: macOS x86_64, 6 logical CPUs, .NET SDK 10.0.302. The baseline was
`f6eaf6a9`; the repaired implementation was `ba79f470`. Both were run with the
same strict CLI command, `--ensure-built`, JSON output and `analysis-profile/v1`
profile destination.

The manual reproducer is the explicit test
`RepositoryMetricsLargeSolutionEvidenceTests`. It requires a baseline CLI DLL
and is intentionally excluded from normal CI/test gates:

```text
ARCHLINERNET_990_BASELINE_CLI=/path/to/baseline/ArchLinterNet.Cli.dll \
ARCHLINERNET_990_EVIDENCE_DIR=/tmp/arch-linter-net-990-evidence \
dotnet test tests/ArchLinterNet.Core.Tests --no-restore \
  --filter FullyQualifiedName~RepositoryMetricsLargeSolutionEvidenceTests
```

## Baseline versus repaired head

The generated workload intentionally includes 128 layout finding candidates, so
both runs complete with the same expected `ValidationFailure`/exit-code 1
outcome. After normalizing only the unique temporary fixture root and removing
the repository-metrics projection, the canonical JSON result digest is identical
for both runs:

`513cd2d31697f513bd671a8a6ec737731abbda0e76352ff7ae933352a2e7b461`

| Evidence | Baseline `f6eaf6a9` | Repaired `ba79f470` |
|---|---:|---:|
| Project graph evaluations | 1 | 1 |
| Assembly loads | 32 | 32 |
| Fact-index materializations | 1 | 1 |
| Source-scan passes | 1 | 1 |
| Source files scanned | 1,024 | 1,024 |
| Total elapsed | 23,005 ms | 22,673 ms |
| Total processor time | 6,193.744 ms | 6,238.041 ms |
| Allocated bytes | 1,231,569,616 | 1,231,403,656 |

The shared `repository_metrics` phase was measured as:

| Evidence | Baseline | Repaired |
|---|---:|---:|
| Elapsed | 21 ms | 22 ms |
| Processor time | 21.861 ms | 22.291 ms |

The repaired run therefore adds no source traversal or fact-index
materialization beyond the baseline workload. Its repository-metrics phase is
within single-millisecond measurement noise, total wall time is 1.4% lower in
this pair, and allocated bytes are marginally lower. These timing observations
do not claim a universal performance guarantee; the identical counters are the
durable low-cost evidence.

The repaired head emitted complete metrics for the large workload:

| Metric group | Observed values |
|---|---|
| Size | 4,096 source lines; 1,024 source files; 32 projects; 512 types; 512 public types |
| Coupling | 496 dependencies; density 0.5; max fan-in 31; max fan-out 31 |
| Structure | max depth 31; 0 cyclic components; largest SCC 1 |

## Ordinary validation cost boundary

`ArchitectureAnalysisSession.GetRepositoryMetrics()` keeps source inventory
materialization explicit. The default Core projection does not force the lazy
`SourceFileFactIndex`; report/Health paths request source inventory only when
their output contract requires absolute source-size evidence. The regression
test `Calculator_DoesNotMaterializeConfiguredSourceFactsWithoutExplicitRequest`
verifies that an ordinary projection remains partial and that explicit opt-in
performs exactly one materialization.

The #502 real-MSBuild sample above exercises the expensive boundary directly:
both baseline and repaired runs perform exactly one source scan over all 1,024
files, with no second MSBuild or semantic-analysis pass attributable to the
metrics projection.
