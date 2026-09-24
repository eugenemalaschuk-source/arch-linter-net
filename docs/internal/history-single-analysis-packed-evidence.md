# History single-analysis packed evidence

- Issue: `#1016`
- Source revision: `ee3140819973bf1aa07280de80b46ab93cc6fce8+dirty`
- Tool package SHA-256: `bcd51bbab4fcd26113ba5468712bdaf7f9445833c71246dde6bedcbcb05e489c`
- Fixture range: `72e4c21cf1abc90f916fe01b050f65a35c3d2bea` → `eb2824f3f7716c339f57852025482ea478fba76b` (5 commits)
- SDK/runtime: 10.0.303 / .NET 10.0.11; Microsoft Windows 10.0.19045; X64

Reproduce from a restored checkout with:

```text
dotnet test tests/ArchLinterNet.Cli.Tests --no-restore --filter FullyQualifiedName~HistorySingleAnalysisPackedBenchmarkHarness -- NUnit.ExplicitTests=true
```

| Shape | Wall ms | Ingestion ms | Scoring ms | JSON ms | Markdown ms | Process overhead ms | Peak working set | Ingestion calls |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Before: two processes | 582.869 | 94.969 | 108.544 | 16.494 | 5.080 | 351.958 | 36683776 | `2,2,2` |
| After: one packed process | 316.031 | 47.018 | 53.201 | 17.113 | 3.119 | 175.968 | 37355520 | `1,1,1` |

- Before is two independent invocations of the same freshly packed and locally installed CLI package (one JSON and one Markdown); after is one invocation with two file sinks.
- The package SHA-256 identifies the exact packed tool used by every before/after sample.
- Phase timings come from --timings. process_overhead_ms is wall clock minus reported policy/ingestion/scoring/render/output phases.
- Measurements are runner evidence, not a promise that a complete release job is exactly twice as fast.
