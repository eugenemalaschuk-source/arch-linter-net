# Issue #1147: Cycle recorder workload evidence

**Disposition: insufficient evidence to authorize an adaptive fallback.** Keep the SCC
implementation from #1146 while collecting representative consumer workloads. The approved
ArchLinterNet self-analysis is internal dogfood only; it has a small, high-evidence cycle graph and
does not estimate consumer frequency. Synthetic stress cases show that one short legacy traversal
can beat building the full SCC index, but do not establish how often that shape occurs.

This is post-release 0.10.0 performance-stabilization evidence. It does not change release
authority, reopen #1146, or make this work a prerequisite for that PR.

## Workload and reproducibility

The selected workload is ArchLinterNet's own strict/audit architecture-policy self-analysis. No
external consumer repository, private policy, or uploaded telemetry was used. For profile-overhead
measurement, each mode had one warm-up pair and five measured pairs of `--profile` versus
`--timings`-only, alternating order. Both members used the same prepared build receipts and policy
input. Every pair had an identical normalized digest of the ordered full `violations` and `cycles`
arrays, pass result, and mode.

For algorithm comparison, an isolated experimental legacy build and the indexed build ran the same
strict analysis in five alternating pairs. They share the same feature worktree and differ only in
the cycle recorder implementation and its temporary work-counter fields. The audit mode was
checked in one paired control; it produced zero recorder invocations in both variants. The legacy
variant was used only in an isolated worktree and is not part of the production change.

The analysis-time boundary is `ValidationTiming`'s `total` phase. It excludes CLI startup and
profile serialization/rendering. Timing-only analysis totals are parsed from the existing CLI
`--timings` output; process wall time is retained separately. Profile-on allocation and peak working
set are exact `AnalysisProfile` process measurements. Timing-only whole-process allocation and peak
working set are unavailable from the CLI and remain null in the samples. No sampled counter
estimate is used as an exact memory comparison.

The explicit recorder benchmark measures direct `Record` calls after warmup; graph, evidence, and
output construction are outside the timed region. Its five legacy/SCC samples are interleaved in
alternating order, and deterministic traversal counters run outside timing.

| Identity | Value |
|---|---|
| Repository head when runs started | `7f55c50f0757fdcc706c35e06800f1f7cade8295` (#1146 merge baseline; the measured working tree included the source changes identified below) |
| Runtime / SDK | .NET 10.0.13 / SDK 10.0.402 |
| OS / architecture / CPU | Windows 10.0.26300 / x64 / AMD Ryzen 7 255 with Radeon 780M |
| Build configuration | Debug for full analysis; Release for direct recorder benchmark |
| Pinned SDK file | `global.json` remained unchanged; SHA-256 `81ae2087dbfb46027fd94748351fb4b1ab24145fbb52544a2822bf128e2e48f1` |
| Policy input SHA-256 | `3b2b2c6664eb9e598c0bcdbd29af50d63ec89db1f263dc6f910b92843dc3a7ae` |
| Indexed CLI assembly SHA-256 | `b661b04c2865c55794fc71f4d50d0119c35a843f4916329afe6acc0e687643c3` |
| Indexed Core assembly SHA-256 | `27122446fce6617e8de07ecdde967d3fe60be5a66a4830ec37ca2effb769da9b` |
| Legacy CLI assembly SHA-256 | `2714a8392985655bd0d396d2b06f79ed71aa428216fd0ab581b338b5bb6a75ea` |
| Legacy Core assembly SHA-256 | `e7d23ae830f3cb01c608a244a347d36fbe41ca022f468f14310475e89b907711` |
| Measured full-analysis/profile source fingerprint | `46658fc7e6b9508297dfc450114ccebcbad27feecf7d8c93b4001ed3b8170c9d` |
| Direct recorder benchmark source fingerprint | `28ba9bceb8a180e3bde9d5bd5048a3cc3c3dbaf6c9cec583b4bca31c3ce1486c` |
| Final packaged 0.10.0 profile schema SHA-256 (canonical LF) | `022aa51e2f9c73ce76727b52bae079445cc6232c63d5fdd3660ee93eb9d401ed` |

The reproducible source inputs and per-file hashes for these fingerprints are in
[`source-fingerprints.sha256`](source-fingerprints.sha256). Binary hashes identify the exact
indexed and isolated legacy assemblies used by the full-analysis runs. The legacy build changed
only the recorder implementation and temporary work-counter fields; its CLI assembly hash is
retained separately because each isolated worktree produced its own CLI build output.

The measured source fingerprints and binary hashes identify the inputs captured before the
cross-platform package-validation correction. That follow-up normalized the new schema resource to
the repository's canonical LF line endings and updated its manifest digest to the final value above;
it did not change recorder code or the captured measurements. The listed binary hashes continue to
identify the exact binaries that produced the evidence.

The exact x64 SDK 10.0.402 and its .NET 10.0.13 runtime were installed in the per-user .NET
directory. The repository's SDK pin was not changed. Both comparison builds and the explicit Release
benchmark used this SDK/runtime pair.

## Full-analysis profile overhead

All five strict pairs matched the normalized full result digest. Strict completed with exit code 0
and no violations. Audit returned exit code 1 with the same single violation in every pair; audit is
expected to fail validation for this policy input. Profile timing differences are within the
observed pair ranges.

| Mode | Profile total median (range), ms | Timing-only total median (range), ms | Paired profile-minus-control median (range), ms | Exact profile-on allocation, median (range) | Exact profile-on peak WS, median (range) |
|---|---:|---:|---:|---:|---:|
| Strict | 10,229 (10,184–10,274) | 10,172 (9,985–10,427) | 22 (-243–289) | 1,761,628,264 (1,761,569,360–1,766,595,752) B | 222,990,336 B |
| Audit | 3,931 (3,904–3,974) | 3,931 (3,922–4,176) | -18 (-229–43) | 662,872,896 (661,946,848–662,952,384) B | 188,067,840 B |

These pairs show no measurable profile overhead or improvement for this workload. They compare
profiling on/off for the indexed implementation; they are distinct from the legacy/SCC algorithm
comparison below.

### Recorder observations

Across five profiled strict runs, the recorder was called once per run, enabled and nonempty each
time. The joint tuple was `(V,E,B,U,K) = (4,3,1426,3,0)` in every run. Median instrumented recorder
time was 1.5344 ms (range 1.4695–1.5419 ms), including its deterministic SCC work counter. Median
index construction was 0.8211 ms and candidate evaluation was 0.3102 ms. Wrapper observation work
to count graph dimensions and unique pairs had a median of 0.718 ms and allocated 440 B per
invocation. The recorder interval is about 0.015% of the 10,229 ms strict analysis median; wrapper
observation is about 0.007%. These shares describe this dogfood workload only.

Across the five profiled audit runs there were zero recorder invocations (not five empty
invocations). The algorithm-paired audit control also had zero invocations in both variants.

## Legacy versus indexed full-analysis comparison

The five strict pairs had exact parity for the normalized ordered violations and cycles arrays,
pass result, mode, and exit category. The SHA-256 was identical in all ten runs. The complete CLI
JSON also contains repository metrics; `source_lines` differs because the experimental recorder
implementations have different source lengths, so that non-diagnostic metric is excluded from the
semantic parity digest.

| Variant | Full-analysis total median (range), ms | Recorder median (range), ms | Recorder allocated bytes | Full-process allocation median (range) | Peak WS median (range) |
|---|---:|---:|---:|---:|---:|
| Legacy reachability | 10,209 (10,101–10,902) | 0.9575 (0.881–1.8937) | 376,504 B | 1,761,688,168 (1,760,966,584–1,763,590,024) B | 209,088,512 (202,465,280–218,742,784) B |
| SCC index | 10,259 (10,062–10,943) | 1.5272 (1.5181–2.0500) | 1,816 B | 1,761,737,696 (1,749,896,568–1,766,288,600) B | 207,597,568 (202,907,648–219,193,344) B |

The paired SCC-minus-legacy full-analysis difference had a median of **+41 ms** with a range of
**-147 to +158 ms** over roughly 10.2 seconds of analysis. This is within run-to-run variation and
does not demonstrate a material end-to-end winner for this small-graph, high-evidence dogfood case.
The legacy recorder was faster here but allocated about 207× more per call. Full-process allocation
and peak-working-set spreads overlap and are noisy; they do not show a stable whole-process memory
difference at this scale.

## Direct legacy versus SCC recorder comparison

| Input | Evidence / unique pairs | Legacy median ms / B | SCC median ms / B | Observation |
|---|---:|---:|---:|---|
| Fixed graph, V=8 E=8 | 100 / 4 | 0.0232 / 32,504 | 0.0132 / 3,768 | SCC 1.76× faster; 88.4% fewer allocated bytes |
| Fixed graph, V=8 E=8 | 1,000 / 4 | 0.5685 / 320,544 | 0.1268 / 11,008 | SCC 4.48× faster; 96.6% fewer allocated bytes |
| Fixed graph, V=8 E=8 | 10,000 / 4 | 3.9958 / 3,251,520 | 0.6456 / 133,984 | SCC 6.19× faster; 95.9% fewer allocated bytes |
| Sparse cycle, V=10,000 E=10,000 | 1 / 1 | 0.6048 / 673,384 | 3.0504 / 2,204,104 | SCC 5.04× slower; 3.27× allocation |
| Sparse cycle, V=10,000 E=10,000 | 10 / 10 | 4.6212 / 6,731,848 | 1.7283 / 2,204,344 | SCC 2.67× faster; 67.3% fewer allocated bytes |
| Dense cycle, V=500 E=124,751 | 1 / 1 | 0.0129 / 8,808 | 3.0703 / 1,105,848 | SCC 238× slower; 126× allocation |
| Dense cycle, V=500 E=124,751 | 10 / 10 | 13.0252 / 468,768 | 2.4674 / 1,106,088 | SCC 5.28× faster; 2.36× allocation |

The complete five-sample arrays, graph/evidence/output digests, and traversal work counters are in
[`recorder-paired-benchmark.json`](recorder-paired-benchmark.json). Legacy and indexed candidate
arrays matched in order, count, digest, and object identity for every workload. In the dense B=1
case, legacy visited three vertices before finding the return path; the SCC path prepared all 500
vertices and 124,751 edges. These synthetic stress shapes are not consumer-frequency evidence.

## Decision and materiality gate

**Current decision: insufficient evidence to authorize a fallback; retain the indexed production
path.** The self-analysis shows a low-absolute-cost example where legacy is faster per recorder call,
but SCC avoids about 375 KB of per-call allocations and the full-analysis difference is inside the
paired timing range. Its recorder graph has only four vertices and three edges. The synthetic
matrix identifies a possible low-evidence large-graph penalty, but neither its frequency nor its
end-to-end effect in consumer workloads is known.

Do not select a threshold from `B` alone or from synthetic shapes. A separate fallback PR is
justified only after approved consumer-shaped workloads show repeated large-graph/low-evidence calls
and a controlled, exact-parity legacy/SCC full-analysis comparison produces a benefit larger than
the paired timing variation, without a material regression on the same corpus's higher-evidence
workloads. Include the selector's decision cost in that comparison. If those conditions are not
observed, keep SCC. This is an evidence gate, not a numeric threshold inferred from one internal
repository.

**Revisit trigger:** add at least one approved consumer-shaped workload whose recorder captures the
joint `(V,E,B)` distribution and legacy traversal work for repeated low-`B`, large-graph calls. If no
such workload is available, keep the fallback decision explicitly unresolved; missing data is not
evidence that the trade-off is absent.

## Artifacts and reproduction

- [`analysis-paired-runs.json`](analysis-paired-runs.json): sanitized five-pair profile-on/timing-only
  samples for strict and audit, with exact analysis totals, output digest, profile-on allocations and
  peak memory, and recorder summaries. It contains no local profile path or diagnostic identity.
- [`full-analysis-legacy-indexed.json`](full-analysis-legacy-indexed.json): five strict full-analysis
  legacy/SCC pairs plus one audit control and their normalized semantic digests.
- [`profiles/`](profiles/): strict/audit profiles for both measurement series. They retain numeric
  aggregate observations and phase counters only; no topology, local path, or diagnostic identity is
  serialized.
- [`recorder-paired-benchmark.json`](recorder-paired-benchmark.json): complete synthetic direct
  recorder samples and deterministic work.

Run the existing explicit direct-recorder matrix from the repository root after restore with SDK
10.0.402 selected by the repository's `global.json`:

```powershell
$env:ARCHLINTER_CYCLE_RECORDER_SOURCE_IDENTITY = '<source revision or working-tree fingerprint>'
dotnet test tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj `
  --no-restore -c Release `
  --filter 'FullyQualifiedName~RunBaselineCandidateRecorderMatrix' `
  --logger 'console;verbosity=detailed'
```

Full-analysis runs use the existing CLI with the self-analysis architecture policy, prepared
receipts, `--timings`, JSON output and, for profiled members, `--profile <file>`. The report does
not retain local command paths or raw CLI output.
