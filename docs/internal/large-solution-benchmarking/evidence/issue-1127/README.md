# Issue #1127 exact ignore matching evidence

The explicit NUnit harness follows the existing #502 evidence style. It separates deterministic work counts from environment-dependent timing and memory. Both machine-readable matrices are checked in beside this file; the benchmark is excluded from normal tests and CI.

## Measurement setup

- Before matcher: revision `190da651d7c12f5bc3bc2a34239a5c79698ef68d`, production matcher SHA-256 `d7fa23204fd13d212e547772e9a86a1a8fb140bb2fb687d35e80cf5b6c110b6f`.
- Environment: .NET 10.0.15; Windows 10.0.19045; x64; 6 processors.
- Each workload uses five samples, one ignore list per contract, one tracker, and independently varied ignore entries (I), findings (F), required matches (K), and wildcard entries (W).
- `before.json` is the run against the unchanged ordered matcher. Its historical schema predates per-input hashes. `after.json` records each serialized input SHA-256 and verifies every canonical result hash against `before.json`; both runs use the same unchanged workload generator and fixture definitions.
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
| Small identity, I=8 / F=8 / K=8 | 0.0093 ms / 1,024 B | 0.0197 ms / 10,672 B | 2,768 B |
| Identity, I=64 / F=64 / K=64 | 0.3350 ms / 6,272 B | 0.1424 ms / 63,824 B | 16,304 B |
| Identity, I=256 / F=64 / K=64 | 1.1384 ms / 6,272 B | 0.4561 ms / 256,432 B | 64,928 B |
| Identity, I=1,024 / F=64 / K=64 | 4.7136 ms / 6,272 B | 2.0572 ms / 1,063,136 B | 248,816 B |
| Structured waiver, I=64 / F=64 / K=64 | 9.3793 ms / 7,767,168 B | 0.2246 ms / 147,784 B | 5,512 B |
| Structured waiver, I=256 / F=64 / K=64 | 37.8799 ms / 31,049,856 B | 0.2904 ms / 210,088 B | 20,944 B |
| Structured waiver, I=1,024 / F=64 / K=64 | 102.5309 ms / 116,316,288 B | 0.6903 ms / 467,808 B | 80,488 B |
| Literal pair, I=1,024 / F=64 / K=64 | 1.4991 ms / 6,272 B | 0.7224 ms / 384,416 B | 89,312 B |
| Identity finding scale, I=1,024 / F=16 / K=16 | 0.6533 ms / 1,456 B | 1.7772 ms / 1,057,168 B | 248,816 B |
| Identity finding scale, I=1,024 / F=256 / K=256 | 7.9570 ms / 27,184 B | 1.8156 ms / 1,088,656 B | 248,816 B |
| Identity match scale, I=1,024 / F=32 / K=512 | 1.0417 ms / 29,400 B | 1.2805 ms / 629,560 B | 179,936 B |
| Wildcards, I=320 / W=256 / F=64 / K=64 | 90.6649 ms / 57,999,488 B | 106.2235 ms / 58,075,568 B | 22,472 B |

The largest exact-heavy workloads improved for versioned identity, structured waiver, and literal-pair matching. The index reduced the identity case from 65,536 entry evaluations to 1,024 index entries plus 64 exact lookup probes and 64 returned candidates. For the structured-waiver case, fingerprint creation dropped from 65,536 to 64, one per finding. Literal pairs use the same bounded lookup shape.

The cost is front-loaded into index construction and retained state. Small input is about twice as slow and allocates more. With I=1,024 and only F=16 findings, the build dominates; the independent F=64 case improves slightly, while F=256 amortizes the build. Match-count workloads with only F=32 remain slower overall at K=32, 128, and 512, even though deterministic query work grows with K rather than I×F. The full I/F/K axes are in `after.json` and `before.json`.

Wildcard cost remains separate and does not have a linear bound for arbitrary glob patterns. The unmatched double-star workload evaluated W×F patterns (16,384 at W=256) and was slower after indexing; regex work remains dominant there. Allocations were effectively unchanged in that case, while the retained exact-index estimate was about 22 KiB. Wildcard-heavy and small/low-finding regressions are recorded as trade-offs rather than presented as wins.

## Work and end-to-end limits

- Exact preparation processes I entries once. Each finding uses a fixed number of exact dictionary probes; all required exact results and tracker indices add O(K) work. For an alias set, each alias gets lookups, while a live structured-waiver fingerprint is built once for the finding and reused.
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
