# Repository metrics observability

`repository-metrics/v1` is informational evidence about repository size,
coupling and dependency structure. It does not change findings, Health, metric
budgets or the process exit code. For enforcing budgets, use
[declared architecture metrics](../policy-format/architecture-metrics.md).

## Get the evidence

Use the [Health/review workflow](../guides/single-tool-workflow.md) to obtain
Health and architecture-change artifacts. The PR report includes a compatible
base/current metrics delta. A validation artifact may also carry repository
metrics, but source-size data can be partial when that analysis did not need
the lazy source inventory.

The metrics badge command reads a supported artifact containing the metrics,
not an already-rendered Shields payload:

```bash
mkdir -p artifacts/metrics-badges
arch-linter-net badge repository-metrics --input artifacts/health.json \
  --output-directory artifacts/metrics-badges
```

`--output-directory` writes `repository-metrics-badge.json`, `repository.json`
and `structure.json`. For just one Source lines file, use `--output` instead,
not together with `--output-directory`.

Check `badge repository-metrics --help` in the pinned package before adopting
this command; older packages may not contain it. Inspect availability and reason
codes in the source artifact before interpreting a number. Missing old-version
metrics are unavailable, not zero. Do not run every metric separately.

## Absolute metrics

The size group includes physical C# source lines, readable source files,
discovered projects, reflected types and public types. Physical source lines
are not a promise of Visual Studio logical SLOC equivalence. Existing generated-
file exclusions and project ownership apply; overlapping roots do not count a
file twice.

The coupling group contains deduplicated internal project-reference edges,
dependencies per project, directed density, maximum fan-in/fan-out and per-project
rows. Afferent coupling (Ca) is fan-in; efferent coupling (Ce) is fan-out.
Instability is `Ce / (Ca + Ce)`, defined as zero when there are no incident edges.

For more than one project, density is non-self-loop directed edges divided by
`projects * (projects - 1)`; otherwise it is zero. `DependencyCount` still includes
self-loops. This keeps density bounded without hiding cycle evidence.

The structure group includes dependency depth, strongly connected components
(SCCs), largest SCC size/ratio and projects participating in cycles. Self-loops
are cycles. Depth counts the longest directed path in the condensation graph,
with cycles collapsed before counting edges.

## Availability and change reports

Complete evidence requires a complete type universe, source inventory and
project graph. Partial evidence retains safely measured values and reasons for
missing/ambiguous inputs. Unavailable values are null, not zero.

Architecture-change artifacts carry compatible base-to-head deltas. Missing or
incompatible base evidence makes that delta unavailable. `report pr` renders it
in the existing bounded report, not another comment or a new governance decision.

## Badge provenance

Source lines and optional grouped Repository/Structure badges are absolute
snapshots, not PR deltas or quality grades. Health uses its own separate badge.
The upstream repository publishes metrics beside Health on its existing raw
badge branch, using verified producer artifacts. That is a
[repository-specific publication example](repository-ci.md), not a requirement
to create a new publisher in every consumer.

Do not feed the raw branch's rendered `architecture-health.json` Shields file
back into the metrics command as though it were a full Health document. A stable
filename is not a schema identity.

## Performance boundary

Calculation is cached in the immutable analysis session and reuses its type
index, source facts and project graph. It does not add another MSBuild load,
semantic compilation or traversal per metric. A report/Health caller can request
source inventory; a lighter validation may leave source-size data partial.

Legacy cache entries may lack repository metrics. They retain the old artifact
rather than fabricate numbers; an eligible fresh analysis produces current
metrics. Enabling these informational views does not establish that a real
project is cache-eligible or that every workflow will obtain a speedup.
