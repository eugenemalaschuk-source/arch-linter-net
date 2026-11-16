# Issue #1127 exact ignore matching evidence

The explicit NUnit harness follows the existing #502 evidence style. It separates deterministic work counts from environment-dependent timing and memory. Both machine-readable matrices are checked in beside this file; the benchmark is excluded from normal tests and CI.

## Measurement setup

- Before matcher: revision `190da651d7c12f5bc3bc2a34239a5c79698ef68d`, production matcher SHA-256 `d7fa23204fd13d212e547772e9a86a1a8fb140bb2fb687d35e80cf5b6c110b6f`.
- Environment: .NET 10.0.15; Windows 10.0.19045; x64; 6 processors.
- Each workload uses five samples, one ignore list per contract, one tracker, and independently varied ignore entries (I), findings (F), required matches (K), and wildcard entries (W).
- `before.json` is the run against the unchanged ordered matcher. Its historical schema predates per-input hashes. `after.json` records each serialized input SHA-256 and verifies every canonical result hash against `before.json`; both runs use the same unchanged workload generator and fixture definitions.
- `fallback.json` separately compares missing-live-identity queries against the ordered reference and measures compact index build, first lazy fallback query, subsequent queries, allocations, and result parity.
- The old ordered matcher is now retained as a test-only reference, so a later baseline run cannot accidentally time the indexed compatibility entry point.

## Pre-implementation effect contract

- **Expected work:** prepare exact indexes in O(I), then do fixed exact lookups per finding/alias plus O(K) required reporting. Build a live structured-waiver fingerprint at most once per applicable finding. Keep wildcard fallback separate.
- **Success gate:** differential suppression, matched-index, and provenance parity; deterministic work assertions; and a lower combined build-plus-query median on the largest exact-heavy workload for versioned identity, structured waiver, and literal pairs. Report the small-input and wildcard-heavy trade-offs.
- **Kill gate:** reject or revise the index if any canonical result, tracker index, or loaded-baseline provenance differs; exact work still scales as I×F; a live fingerprint is built more than once per applicable finding; or any largest exact-heavy workload fails to improve.
- **Phase share:** the focused inner workload is 100% matcher calls. Product-validation share is **not measured**, so the whole-validation Amdahl bound is also **not measured** and no product-wide speedup is inferred.

## Before and after medians

Elapsed values include matcher/index work only. After values include index construction plus all finding lookups. Retained index bytes are an approximate managed-heap delta while a per-contract index is held; baseline has no retained matcher index.

| Workload | Before time / allocated | After time / allocated | Retained index estimate |
| --- | ---: | ---: | ---: |
| Small identity, I=8 / F=8 / K=8 | 0.0093 ms / 1,024 B | 0.0174 ms / 1,176 B | 72 B |
| Identity, I=64 / F=64 / K=64 | 0.3350 ms / 6,272 B | 0.1061 ms / 16,384 B | 9,824 B |
| Identity, I=256 / F=64 / K=64 | 1.1384 ms / 6,272 B | 0.2069 ms / 84,600 B | 47,048 B |
| Identity, I=1,024 / F=64 / K=64 | 4.7136 ms / 6,272 B | 0.7954 ms / 395,960 B | 211,256 B |
| Structured waiver, I=64 / F=64 / K=64 | 9.3793 ms / 7,767,168 B | 0.2356 ms / 132,536 B | 4,712 B |
| Structured waiver, I=256 / F=64 / K=64 | 37.8799 ms / 31,049,856 B | 0.2946 ms / 163,600 B | 21,632 B |
| Structured waiver, I=1,024 / F=64 / K=64 | 102.5309 ms / 116,316,288 B | 0.6499 ms / 305,184 B | 88,048 B |
| Literal pair, I=1,024 / F=64 / K=64 | 1.4991 ms / 6,272 B | 0.5800 ms / 207,480 B | 109,048 B |
| Identity finding scale, I=1,024 / F=16 / K=16 | 0.6533 ms / 1,456 B | 0.9827 ms / 391,144 B | 211,368 B |
| Identity finding scale, I=1,024 / F=64 / K=64 | 1.9346 ms / 6,272 B | 0.8506 ms / 395,960 B | 227,800 B |
| Identity finding scale, I=1,024 / F=256 / K=256 | 7.9570 ms / 27,184 B | 1.0954 ms / 416,872 B | 211,256 B |
| Identity match scale, I=1,024 / F=32 / K=32 | 0.9761 ms / 29,400 B | 0.8057 ms / 392,688 B | 203,032 B |
| Identity match scale, I=1,024 / F=32 / K=128 | 0.9863 ms / 29,400 B | 0.7915 ms / 401,216 B | 215,096 B |
| Identity match scale, I=1,024 / F=32 / K=512 | 1.0417 ms / 29,400 B | 1.2282 ms / 232,040 B | 105,736 B |
| Wildcards, I=320 / W=256 / F=64 / K=64 | 90.6649 ms / 57,999,488 B | 4.3854 ms / 953,152 B | 401,624 B |

The largest exact-heavy workloads improved for versioned identity, structured waiver, and literal-pair matching. The index reduced the identity case from 65,536 entry evaluations to 1,024 index entries plus 64 exact lookup probes and 64 returned candidates. For structured waivers, fingerprint creation dropped from 65,536 to 64, one per finding. The first candidate for each identity is stored inline; duplicate candidates use a compact overflow array. A bounded initial dictionary capacity reduces repeated backing-array growth without reserving the full ignore-list size for every category. In the current run, I=1,024/F=64 identity matching took 0.7954 ms versus 4.7136 ms before indexing; allocations fell 18.7% from the previous after run (486,720 to 395,960 B), while the retained estimate fell from 259,376 to 211,256 B. Timing varies between runs, so the checked-in JSON records the latest five-sample medians.

Small live lists (I≤8) stay on the ordered path. In this run I=8/F=8 measured 0.0174 ms versus 0.0093 ms before indexing, with allocations of 1,176 B versus 1,024 B; this sub-0.02 ms result is sensitive to machine noise. Most exact-heavy rows improved, but two short high-I rows remain slower in this run: I=1,024/F=16 measured 0.9827 ms versus 0.6533 ms, and F=32/K=512 measured 1.2282 ms versus 1.0417 ms. Other duplicate-heavy rows improved. Timing varies between runs; the complete axes are in `after.json` and `before.json`.

Wildcard matching now reuses each normalized pattern matcher from the index. In the unmatched double-star workload, 16,384 wildcard candidates took 4.3854 ms after indexing versus 90.6649 ms before; allocations fell from 57,999,488 B to 953,152 B. The retained estimate for the compiled wildcard index was about 392 KiB. These measurements cover this workload's double-star patterns; arbitrary glob runtimes remain pattern-dependent.

## Work and end-to-end limits

- The first query with a live identity lazily prepares the exact indexes in O(I), except lists of at most eight entries which stay ordered. Subsequent indexed findings use a fixed number of exact dictionary probes; reporting all K matches and tracker indices adds O(K). A live structured-waiver fingerprint is built once per finding and reused for aliases. Distinct glob patterns are normalized and prepared once with the index, then reused with the existing regex timeout.
- Queries without a live identity use the ordered matcher directly at first. After cumulative ordered scanning reaches 4×I entries, the matcher requests the literal/wildcard fallback index; the next query builds it once and later queries reuse it. This keeps short or early-match checks on the no-index path while amortizing long repeated workloads. All live-identity matrix rows report zero missing-identity scan or fallback-build work.
- `fallback.json` measures two I=1,024/F=64 cases without live identities. For early matches, the ordered path took 0.2801 ms and allocated 120 B; the adaptive path took 0.2148 ms and allocated 232 B without building an index. For 64 misses, the ordered path took 6.5128 ms and allocated 120 B; the adaptive path took 0.8699 ms and allocated 201,152 B. The first four misses scan 4×I entries, the fifth query builds the fallback index, and subsequent misses use it. The complete workload is about 7.5× faster, with an allocation cost.
- Wildcard fallback remains separately attributable as O(F×W) candidate evaluation plus pattern-matching cost. These measurements use unmatched double-star patterns and do not generalize regex runtime for arbitrary globs.
- The focused workload is 100% matcher calls. Product-validation phase share is **not measured** because the current profile does not attribute matcher time separately. A whole-validation Amdahl bound and product-wide speedup are therefore **not measured**.
- Retained-index estimates use full collections around a held index, exclude the input list, and are noisy at small sizes. They are not process peak working-set measurements.

## Commands

Run the before reference matrix:

```powershell
dotnet test tests/ArchLinterNet.Core.Tests --no-restore -c Release --filter "FullyQualifiedName~RunCurrentMatcherBaseline" --logger "console;verbosity=normal" -- NUnit.ExplicitTests=true
```

Run the after matrix and compare canonical result hashes:

```powershell
dotnet test tests/ArchLinterNet.Core.Tests --no-restore -c Release --filter "FullyQualifiedName~RunAfterMatcherEffectMatrix" --logger "console;verbosity=normal" -- NUnit.ExplicitTests=true
```

Run the explicit first-and-repeated deferred fallback measurement:

```powershell
dotnet test tests/ArchLinterNet.Core.Tests --no-restore -c Release --filter "FullyQualifiedName~RunDeferredFallbackMatcherBenchmark" --logger "console;verbosity=normal" -- NUnit.ExplicitTests=true
```
