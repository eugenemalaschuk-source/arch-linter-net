# Issue #990 repository-metrics projection evidence

## Scope

This is focused profiling evidence for the repository-metrics observability
projection. It demonstrates that the projection reuses the immutable analysis
session facts and does not perform a second MSBuild or semantic-analysis pass.
It is repository-specific evidence, not a universal performance guarantee.

## Command and environment

The baseline and head measurements used the same strict CLI path, repository
checkout, build-preparation boundary, and JSON/profile sinks:

```text
dotnet run --project src/ArchLinterNet.Cli -- \
  --policy architecture/dependencies.arch.yml \
  --mode strict --format json --ensure-built \
  --profile /tmp/archlinter-990-profile-<revision>.json
```

The baseline was commit `8a692af7` (the pre-fix PR head); the head measurement
was the repaired working tree on 2026-09-22. Both profiles were
`analysis-profile/v1` with `CompletionStatus: Success`.

## Observed deterministic work

| Counter | Baseline `8a692af7` | Head |
|---|---:|---:|
| Discovered projects | 4 | 4 |
| Selected assemblies | 4 | 4 |
| Fact-index materializations | 1 | 1 |
| Source-scan passes | 1 | 1 |
| Source files scanned | 1,016 | 1,016 |
| Assembly loads | 4 | 4 |
| Snapshot materializations | 1 | 1 |

The emitted repository-metrics snapshots were complete. The baseline contained
124,092 source lines and the head contained 124,196 source lines (the delta is
the repaired implementation itself); both contained 1,016 source files, 4
projects, 2,981 reflected types, and 647 reflected public types. Both project
graphs contained 3 deduplicated dependency edges with no cyclic components.

## Observed phase

The shared projection was recorded as the `repository_metrics` phase:

| Phase | Baseline | Head |
|---|---:|---:|
| `repository_metrics` elapsed | 19 ms | 20 ms |
| `repository_metrics` processor time | 19.653 ms | 20.799 ms |

The profile contains one source scan, one fact-index materialization, and one
repository-metrics phase in both revisions. These counters are the correctness
evidence for the shared projection boundary; the timing is only an observation
for this local run.

## Ordinary validation cost boundary

The CLI health/report paths explicitly request source inventory because their
contract is to publish absolute source-size evidence. Ordinary callers leave
`IncludeRepositoryMetrics` at its default `false`. The regression test
`Calculator_DoesNotMaterializeConfiguredSourceFactsWithoutExplicitRequest`
configures a source root, requests the default projection, and verifies that
the lazy fact index remains unmaterialized, source lines are unavailable with
the typed `source_inventory_not_materialized` reason, and explicit opt-in then
performs the single expected materialization. This is the low-cost acceptance
boundary: an informational projection cannot silently turn policy-only
validation into a recursive source scan.

The repository run above is deliberately not presented as a #502 large-
solution timing claim. The existing #502 workload remains the scale-test
contract; this evidence supplies a reproducible before/after counter check on
the actual 1,016-file repository and a direct regression test for the lazy
boundary.
