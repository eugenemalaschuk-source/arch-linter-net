# Issue #990 repository-metrics projection evidence

## Scope

This is focused profiling evidence for the repository-metrics observability
projection. It demonstrates that the projection reuses the immutable analysis
session facts and does not perform a second MSBuild or semantic-analysis pass.
It is one local repository measurement, not a universal performance guarantee.

## Command and environment

The measurement used the strict CLI path with the existing build preparation
boundary and JSON/profile sinks:

```text
dotnet run --project src/ArchLinterNet.Cli -- \
  --policy architecture/dependencies.arch.yml \
  --mode strict --format json --ensure-built \
  --profile /tmp/archlinter-990-profile-v3.json
```

The command completed successfully after the reviewed public API snapshot was
updated. The profile was `analysis-profile/v1` and reported `CompletionStatus: Success`.

## Observed deterministic work

| Counter | Value |
|---|---:|
| Discovered projects | 4 |
| Selected assemblies | 4 |
| Fact-index materializations | 1 |
| Source-scan passes | 1 |
| Source files scanned | 1,016 |
| Assembly loads | 4 |
| Snapshot materializations | 1 |

The emitted repository-metrics snapshot was complete and contained 124,092
source lines, 1,016 source files, 4 projects, 2,981 reflected types, and 647
reflected public types. The project graph contained 3 deduplicated dependency
edges, with no cyclic components.

## Observed phase

The shared projection was recorded as the `repository_metrics` phase:

| Phase | Elapsed | Processor time |
|---|---:|---:|
| `repository_metrics` | 20 ms | 20.442 ms |

The profile contains one source scan, one fact-index materialization, and one
repository-metrics phase. These counters are the correctness evidence for the
shared projection boundary; the timing is only an observation for this local
run.
