# History single-analysis packed evidence

- Issue: `#1016`
- Source revision: `a328fbcf9e236ab6806a5a4457a1c93ccc425da2`
- Tool package SHA-256: `7375e42f15517573669858a13c9e4f1f2e89c16537727dfd0624f7809f469bd5`
- Fixture range: `3a03e960f2d75520e384a176d497d1f040eab36a` → `ac64a238283d14f53e110649b444cf811adfe8c5` (5 commits)
- SDK/runtime: 10.0.303 / .NET 10.0.11; Microsoft Windows 10.0.19045; X64

Reproduce from a restored checkout with:

```text
dotnet test tests/ArchLinterNet.Cli.Tests --no-restore --filter FullyQualifiedName~HistorySingleAnalysisPackedBenchmarkHarness -- NUnit.ExplicitTests=true
```

| Shape | Wall ms | Ingestion ms | Scoring ms | JSON ms | Markdown ms | Process overhead ms | Peak working set | Ingestion calls |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Before: two processes | 578.160 | 100.753 | 105.650 | 16.155 | 5.784 | 344.072 | 36483072 | `2,2,2` |
| After: one packed process | 331.209 | 45.723 | 54.567 | 18.569 | 3.454 | 189.525 | 37466112 | `1,1,1` |

- Before is two independent invocations of the same freshly packed and locally installed CLI package (one JSON and one Markdown); after is one invocation with two file sinks.
- The package SHA-256 identifies the exact packed tool used by every before/after sample.
- Phase timings come from --timings. process_overhead_ms is wall clock minus reported policy/ingestion/scoring/render/output phases.
- Measurements are runner evidence, not a promise that a complete release job is exactly twice as fast.
