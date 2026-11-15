# Issue #1126: Indexed baseline contract and ignore merge evidence

## Scope and pre-change effect contract

Issue #1126 targets the baseline merge in `ArchitectureBaselineLoadingService.ContractGroupMerger`.
The pre-change path linearly scans the selected contract group for every baseline entry and then
linearly scans the growing ignore list for each eligible ignore. This evidence isolates that merge
boundary with the same workload for the legacy reference and indexed implementation. The legacy
reference is a test-only copy of the old strict-group behavior; it is not production code.

Variables are independent: `C` is the number of contracts, `B` the baseline-entry count, and `I`
the total of inline plus loaded ignores on the selected contract. The expected work changes from
`O(C*B + I*(inline + accepted-loaded))` scans to `O(C + B + I)`, with `O(C + I)` temporary
indexes and the existing ordered output list. Contract indexing preserves first match under
`OrdinalIgnoreCase`. Ignore keys preserve the version-1 exact display pair and the existing
structured identity comparison, including its current-version restriction and its deliberate
omission of `Kind` and `ForbiddenReference`.

The pre-implementation decision contract is:

- Any ordered output digest mismatch rejects the change.
- Deterministic work must be one index insertion per contract, one dictionary lookup per ordinary
  baseline entry, one inline-key seed per existing ignore inspected for a touched contract, and one
  membership check per eligible loaded ignore. Applicability entries do not create a set or count as
  membership checks.
- At the largest ignore point, `I=2,000`, both v1 and v3 must be at least 2x faster in direct merge
  median time, and indexed allocation must stay below 3x the pre-change allocation.
- At the small v1 and v3 points, indexed merge must not be more than 2x slower. If semantic parity
  or linear operation counts fail, reject immediately. If the large-scale speedup is below 1.25x in
  both versions, or allocation exceeds 3x, stop and reassess the index design. Results between the
  success and kill thresholds require investigation rather than a pass.

The integrated CLI exposes a composite `baseline_loading` phase, not merge-only attribution. Its
share is reported as a conservative upper bound because that phase also includes YAML decoding and
loaded-text identity work. It cannot be used as a merge-only speedup claim.

## Workload and method

The explicit Core test `ArchitectureBaselineMergePerformanceEvidenceTests.RunBaselineMergeEffectMatrix`
uses the same generated model objects and input dimensions for each run. It compares the current
`ArchitectureBaselineLoadingService.Merge` output digest to a test-only copy of the old linear
merge. Two warmups precede five Release samples. Direct timing starts immediately before `Merge`
and stops immediately after it; SHA-256 serialization is outside the timed interval. Allocation is
measured with `GC.GetAllocatedBytesForCurrentThread`; retained managed deltas use forced collections
around the resulting policy object. That retained delta is an unstable GC observation, not peak
temporary memory or a process working-set measurement.

Independent scale series are: `C = 32/128/512` with `B=64`; `B = 32/128/512` with `C=1`; and
`I = 100/500/2,000` with equal inline/loaded halves for both v1 and v3. Small v1 and v3 cases use
`C=8`, `B=8`, two inline ignores, and four loaded ignores. Contract lookup targets the last contract
in its group, which makes the old linear lookup do the maximum comparisons at each `C` point. All
loaded ignores are unique, so the output digest checks their ordering, provenance, and complete
identity as well as their presence.

The baseline source revision is `a293f874292f1d4a40f6a720e58efd1605e5f603`; the unchanged
pre-index service file blob was `dfed5fb4a896a977a5e54a02afabb0dab8dbc606`. Environment: .NET SDK
10.0.402, runtime .NET 10.0.15, Windows 10.0.19045 x64, Intel Core i5-9600K CPU @ 3.70GHz,
6 reported processors. The test's environment field records runtime, OS, architecture, and reported
processor count on every row.

## Pre-change results

Each direct time and allocation below is the median of five warmed Release samples. The comparison
counts are deterministic counts from the legacy reference. The `I` workload uses one contract and
one baseline entry.

| Shape | C | B | Inline / loaded | Legacy ID comparisons | Legacy ignore comparisons | Merge median ms | Allocated bytes | Retained output delta bytes | Ordered output SHA-256 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Small v1 | 8 | 8 | 2 / 4 | 8 | 14 | 0.0740 | 11,456 | 768 | `32e96c59f5475c83bf098e23e0bf3b8405105aab389d904e844d914e29a683e3` |
| Small v3 | 8 | 8 | 2 / 4 | 8 | 14 | 0.0733 | 11,456 | 768 | `1bf87da9faaeb3e0cd9f13c0cff5ccf28de31fc866e70eaeddbdb9c71fb08252` |
| Contracts | 512 | 64 | 0 / 0 | 32,768 | 0 | 1.5582 | 19,240 | -4,032 | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` |
| Entries | 1 | 512 | 0 / 0 | 512 | 0 | 0.1863 | 54,576 | 0 | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` |
| v1 ignores | 1 | 1 | 50 / 50 | 1 | 3,725 | 0.1520 | 24,256 | 10,304 | `45b4d6bf4db3fbc6a04a7c34205d23934c1078930538edfcd4a3e0aedbd03094` |
| v3 ignores | 1 | 1 | 50 / 50 | 1 | 3,725 | 0.2515 | 24,256 | 9,712 | `1eef028e9179a6f258bcd8b365cb58d1a412562afbf4d6b0c30d4f686b518475` |
| v1 ignores | 1 | 1 | 250 / 250 | 1 | 93,625 | 1.4757 | 81,728 | 48,048 | `8b7a7d2b1ddbb43f0ef3a4291b34528d0cebbc55171044685a3f6260802adcc9` |
| v3 ignores | 1 | 1 | 250 / 250 | 1 | 93,625 | 4.0633 | 81,728 | 48,048 | `5ba8f6fadd7bc55ae1d4aae2cc7428280e18416223c786311cda2df163fa6d28` |
| v1 ignores | 1 | 1 | 1,000 / 1,000 | 1 | 1,499,500 | 32.8129 | 298,016 | 192,192 | `7a5a63f5c96776aef883ab22280d96c2c0f64736b1cd0e2c00361b056d3acd66` |
| v3 ignores | 1 | 1 | 1,000 / 1,000 | 1 | 1,499,500 | 14.0231 | 298,016 | 192,192 | `ffcc9a39a2ef6862363b4c197353a190b5184c477597551bc5da852e50552399` |

The legacy ignore comparison counts grow from 3,725 to 93,625 to 1,499,500 as `I` grows from
100 to 500 to 2,000. The new structural target for the largest ignore workload is 1,000 inline
entries inspected once plus 2,000 constant-time membership checks. The measured retained deltas
include required loaded output objects and are noisy; the negative contract-only delta demonstrates
that this method cannot report temporary index peak memory. No peak working-set value was captured.

## Baseline phase share

For the integrated phase sample, the generated CLI policy/baseline fixture has one target contract,
1,000 inline structured ignores, and 1,000 loaded structured ignores. Generate it after building
Release with:

```powershell
powershell -NoProfile -File docs/internal/large-solution-benchmarking/issue-1126-generate-phase-share-fixture.ps1
```

Then run the command five times in the same Release build:

```powershell
dotnet src/ArchLinterNet.Cli/bin/Release/net10.0/ArchLinterNet.Cli.dll `
  --policy .benchmark/issue-1126-phase-share/policy.yml `
  --mode audit `
  --baseline .benchmark/issue-1126-phase-share/baseline.yml `
  --timings
```

| Sample | Total ms | `baseline_loading` ms | Composite phase share | Whole-run Amdahl limit if that entire phase vanished |
|---:|---:|---:|---:|---:|
| 1 | 1,431 | 471 | 32.9% | 1.49x |
| 2 | 1,600 | 543 | 33.9% | 1.51x |
| 3 | 1,389 | 303 | 21.8% | 1.28x |
| 4 | 1,472 | 489 | 33.2% | 1.50x |
| 5 | 1,439 | 473 | 32.9% | 1.49x |
| Median | 1,439 | 473 | 32.9% | 1.49x |

The median composite `baseline_loading` share gives `1/(1-p) = 1.49x` only as a generous upper
bound for a hypothetical removal of all baseline-loading work. Merge is a subset of this phase;
the baseline decoder and content identity remain. Timing variance is visible in sample 3. These CLI
numbers do not substitute for the direct merge measurements and do not predict a 1.49x product
speedup.

## Indexed results and effect decision

The same Release matrix was rerun after the implementation, in the same environment, with the same
five-sample direct merge boundary and generated input dimensions. Every indexed digest matched the
legacy digest. The indexed structural counters were exactly `C` contract index entries, `B` contract
lookups, `inline` inline-key index entries for the touched contract, and `loaded` membership checks
and accepts for the unique loaded-ignore inputs.

| Shape | Legacy median ms | Indexed median ms | Local speedup | Legacy / indexed allocated bytes | Legacy scan work | Indexed work |
|---|---:|---:|---:|---:|---:|---|
| Small v1 | 0.0740 | 0.0512 | 1.45x | 11,456 / 5,144 | 8 ID / 14 ignore comparisons | 8 index / 8 lookup / 2 seed / 4 membership |
| Small v3 | 0.0733 | 0.0581 | 1.26x | 11,456 / 5,704 | 8 ID / 14 ignore comparisons | 8 index / 8 lookup / 2 seed / 4 membership |
| Contracts, C=512 B=64 | 1.5582 | 0.0878 | 17.75x | 19,240 / 54,824 | 32,768 ID comparisons | 512 index / 64 lookups |
| Entries, C=1 B=512 | 0.1863 | 0.1045 | 1.78x | 54,576 / 2,856 | 512 ID comparisons | 1 index / 512 lookups |
| v1 ignores, I=100 | 0.1520 | 0.0907 | 1.68x | 24,256 / 18,776 | 3,725 ignore comparisons | 50 seed / 50 membership |
| v3 ignores, I=100 | 0.2515 | 0.1228 | 2.05x | 24,256 / 29,416 | 3,725 ignore comparisons | 50 seed / 50 membership |
| v1 ignores, I=500 | 1.4757 | 0.2333 | 6.33x | 81,728 / 79,200 | 93,625 ignore comparisons | 250 seed / 250 membership |
| v3 ignores, I=500 | 4.0633 | 0.3893 | 10.44x | 81,728 / 130,944 | 93,625 ignore comparisons | 250 seed / 250 membership |
| v1 ignores, I=2,000 | 32.8129 | 0.4812 | 68.19x | 298,016 / 299,824 | 1,499,500 ignore comparisons | 1,000 seed / 1,000 membership |
| v3 ignores, I=2,000 | 14.0231 | 0.9548 | 14.69x | 298,016 / 492,240 | 1,499,500 ignore comparisons | 1,000 seed / 1,000 membership |

Pre-sizing each lazy membership set from the existing inline-list count reduced the large v3
allocation from the first indexed sample's 849,144 bytes to 492,232 bytes. That is 1.65x the
pre-change allocation and below the 3x limit. The largest v1 case allocated within 0.6% of the
pre-change value. The C=512 contract-only case allocated 2.85x its pre-change value because the
group lookup table itself is the measured workload; this is transient O(C) storage and remains
below 3x in this workload.

The local effect contract passes: all outputs match, work counters are linear, both I=2,000 cases
exceed the 2x target, both small cases are faster, and the largest ignore allocations are below
3x. Forced-GC retained output deltas stayed about 192 KB for the I=2,000 cases before and after,
because the membership indexes are local to the merge and are not retained. That observation does
not measure their peak temporary memory. Per-call allocated bytes include both those transient
indexes and the required loaded output objects; process peak working set was not measured.

The same five-sample CLI `--timings` fixture was rerun after the final helper extraction and Release
rebuild:

| Sample | Total ms | `baseline_loading` ms | Composite phase share | Whole-run Amdahl limit if that entire phase vanished |
|---:|---:|---:|---:|---:|
| 1 | 1,531 | 446 | 29.1% | 1.41x |
| 2 | 1,650 | 423 | 25.6% | 1.34x |
| 3 | 1,741 | 545 | 31.3% | 1.46x |
| 4 | 1,657 | 493 | 29.8% | 1.42x |
| 5 | 1,661 | 483 | 29.1% | 1.41x |
| Median across samples | 1,657 | 483 | 29.1% | 1.41x |

The baseline `baseline_loading` median was 473 ms (32.9% median share) and its generous
whole-phase Amdahl limit was 1.49x. The final indexed sample measured 483 ms (29.1% median share);
whole CLI total medians were 1,439 ms before and 1,657 ms after. This composite phase did not show
an end-to-end time reduction in this noisy, single synthetic comparison, even though the isolated
merge improved sharply. The result is consistent with merge being a small part of decode, identity,
and analysis work in this fixture. It does not isolate merge from decoding and identity work or
predict a general consumer speedup.

## Reproduction

With restore assets present, run the direct matrix before or after the implementation:

```powershell
dotnet test tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj --no-restore -c Release `
  --filter FullyQualifiedName~RunBaselineMergeEffectMatrix `
  --logger 'console;verbosity=detailed'
```

The test is `[Explicit]` and stays out of routine test and acceptance runs. Its JSON rows include
the workload dimensions, current direct medians, allocations, retained deltas, deterministic legacy
scan counts, canonical digests, and environment. Rerun the five-command CLI sample above against a
fixture with the same policy/baseline shape to refresh the composite phase share.

## Validation

Commands run after the final helper extraction:

| Command | Result |
|---|---|
| `make fmt` | Passed. The formatter's unrelated markdown whitespace changes were reverted. |
| `dotnet test tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~ArchitectureBaselineMergeIndexTests|FullyQualifiedName~ArchitectureBaselineMergerTests|FullyQualifiedName~ArchitectureBaselineLoaderTests|FullyQualifiedName~ArchitectureBaselineIntegrationTests|FullyQualifiedName~ArchitectureApplicabilityBaselineLifecycleTests|FullyQualifiedName~ImportedExternalDiagnosticProjectionTests|FullyQualifiedName~ExternalDiagnosticsFederationReferenceScenarioTests'` | Passed, 83/83. |
| `dotnet test tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj --no-restore -c Release --filter FullyQualifiedName~RunBaselineMergeEffectMatrix --logger 'console;verbosity=detailed'` | Passed; every indexed digest matched the legacy reference and the measured effect criteria passed. |
| `dotnet test tests/ArchLinterNet.Core.Tests --no-restore -c Release --filter FullyQualifiedName~HistoryLayoutRules_RejectAnExceptionAndInterfaceOutsideTheirConventionalDirectories` | Passed, 1/1 when run alone. |
| `dotnet test tests/ArchLinterNet.Core.Tests --no-restore -c Release --settings .benchmark/issue-1126-core-serial.runsettings` | Completed with 4,234 passed, 16 skipped, and 8 failed. All eight failures were self-policy negative tests whose Debug rebuilds could not overwrite `src/ArchLinterNet.Cli/bin/Debug/net10.0/ArchLinterNet.Cli.dll` because the same testhost held it (`MSB3021`/`MSB3027`); their expected-finding assertions then failed on the build-state preflight result. The isolated layout case above passed. |
| `make lint-code-size` | Passed; the baseline loading service is 733 lines. |
| `make lint-architecture` | Passed strict self-policy validation; no cycles and project coverage complete. |
| `make public-api-check` | Passed for Core, Testing, and CEL snapshots. |
| `make lint-docs` | Passed, 43 checks; MkDocs static build completed. |
| `openspec archive index-baseline-contract-ignore-merging --yes` | Passed; synchronized the `baseline-generation` spec and archived the change as `2026-11-15-index-baseline-contract-ignore-merging`. |
| `openspec validate --all` | Passed, 175/175 items. Existing repository-wide warnings remain non-fatal. |
| `git diff --check` | Passed. |

The full Core result is recorded rather than treated as green. The targeted merge tests, benchmark,
architecture gate, API snapshot check, and documentation check passed; the remaining failures arise
in self-policy tests that rebuild shared Debug outputs from inside the already-running testhost.
