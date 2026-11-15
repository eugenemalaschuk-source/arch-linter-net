# Issue #1126: Indexed baseline contract and ignore merge evidence

## Scope and pre-change effect contract

Issue #1126 targets the baseline merge in `ArchitectureBaselineLoadingService.ContractGroupMerger`.
The pre-change path iterates every contract group, materializes each group's contract list, linearly
scans the selected group for every baseline entry, and then linearly scans the growing ignore list
for each eligible ignore. The test-only reference models that full outer merge path for the same
workload as the indexed implementation; it is not production code.

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
`ArchitectureBaselineLoadingService.Merge` output digest to a test-only copy of the old full outer
merge path. Each implementation gets one warmup; five Release samples then alternate execution
order. Direct timing includes the merge path and group iteration but excludes SHA-256 output
serialization. Allocation is measured with `GC.GetAllocatedBytesForCurrentThread`; current retained
managed deltas use forced collections around the resulting policy object. That retained delta is an
unstable GC observation, not peak temporary memory or a process working-set measurement.

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

Each direct time and allocation below is the median of five warmed Release samples from the full
pre-index reference path. The comparison counts are deterministic. Retained output deltas are not
reported for the legacy reference; the `I` workload uses one contract and one baseline entry.

| Shape | C | B | Inline / loaded | Legacy ID comparisons | Legacy ignore comparisons | Merge median ms | Allocated bytes | Ordered output SHA-256 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Small v1 | 8 | 8 | 2 / 4 | 8 | 14 | 0.0445 | 6,336 | `32e96c59f5475c83bf098e23e0bf3b8405105aab389d904e844d914e29a683e3` |
| Small v3 | 8 | 8 | 2 / 4 | 8 | 14 | 0.0563 | 6,336 | `1bf87da9faaeb3e0cd9f13c0cff5ccf28de31fc866e70eaeddbdb9c71fb08252` |
| Contracts | 512 | 64 | 0 / 0 | 32,768 | 0 | 2.1839 | 14,920 | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` |
| Entries | 1 | 512 | 0 / 0 | 512 | 0 | 0.1100 | 53,840 | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` |
| v1 ignores | 1 | 1 | 50 / 50 | 1 | 3,725 | 0.2889 | 15,032 | `45b4d6bf4db3fbc6a04a7c34205d23934c1078930538edfcd4a3e0aedbd03094` |
| v3 ignores | 1 | 1 | 50 / 50 | 1 | 3,725 | 0.1900 | 15,032 | `1eef028e9179a6f258bcd8b365cb58d1a412562afbf4d6b0c30d4f686b518475` |
| v1 ignores | 1 | 1 | 250 / 250 | 1 | 93,625 | 1.9256 | 54,904 | `8b7a7d2b1ddbb43f0ef3a4291b34528d0cebbc55171044685a3f6260802adcc9` |
| v3 ignores | 1 | 1 | 250 / 250 | 1 | 93,625 | 3.3220 | 54,904 | `5ba8f6fadd7bc55ae1d4aae2cc7428280e18416223c786311cda2df163fa6d28` |
| v1 ignores | 1 | 1 | 1,000 / 1,000 | 1 | 1,499,500 | 65.6254 | 205,192 | `7a5a63f5c96776aef883ab22280d96c2c0f64736b1cd0e2c00361b056d3acd66` |
| v3 ignores | 1 | 1 | 1,000 / 1,000 | 1 | 1,499,500 | 89.6731 | 205,192 | `ffcc9a39a2ef6862363b4c197353a190b5184c477597551bc5da852e50552399` |

The legacy ignore comparison counts grow from 3,725 to 93,625 to 1,499,500 as `I` grows from
100 to 500 to 2,000. The new structural target for the largest ignore workload is 1,000 inline
entries inspected once plus 1,000 membership checks for the 1,000 loaded entries. No peak
working-set value was captured.

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
| -----: | -------: | --------------------: | --------------------: | ---------------------------------------------------: |
|      1 |    1,431 |                   471 |                 32.9% |                                                1.49x |
|      2 |    1,600 |                   543 |                 33.9% |                                                1.51x |
|      3 |    1,389 |                   303 |                 21.8% |                                                1.28x |
|      4 |    1,472 |                   489 |                 33.2% |                                                1.50x |
|      5 |    1,439 |                   473 |                 32.9% |                                                1.49x |
| Median |    1,439 |                   473 |                 32.9% |                                                1.49x |

The median composite `baseline_loading` share gives `1/(1-p) = 1.49x` only as a generous upper
bound for a hypothetical removal of all baseline-loading work. Merge is a subset of this phase;
the baseline decoder and content identity remain. Timing variance is visible in sample 3. These CLI
numbers do not substitute for the direct merge measurements and do not predict a 1.49x product
speedup.

## Revised results and effect decision

The same Release matrix was rerun after the implementation and review refinements in the same
environment, with five alternating samples and the same generated input dimensions. Every current
digest matched the legacy digest. Multi-ID groups build one first-match contract index; groups with
up to eight entries sharing one ID reuse one first-match lookup; imported-only entries trigger no
contract index. Small ignore lists use linear membership checks and larger lists use a set.

| Shape                 | Legacy median ms | Current median ms | Legacy/current | Legacy / current allocated bytes |             Legacy scan work | Current work                                                                      |
| --------------------- | ---------------: | ----------------: | -------------: | -------------------------------: | ---------------------------: | --------------------------------------------------------------------------------- |
| Small v1              |           0.0445 |            0.0464 |          0.96x |                    6,336 / 3,856 | 8 ID / 14 ignore comparisons | 1 linear contract comparison / 14 linear ignore comparisons / 4 membership checks |
| Small v3              |           0.0563 |            0.0705 |          0.80x |                    6,336 / 3,856 | 8 ID / 14 ignore comparisons | 1 linear contract comparison / 14 linear ignore comparisons / 4 membership checks |
| Contracts, C=512 B=64 |           2.1839 |            0.1079 |         20.24x |                  14,920 / 54,904 |        32,768 ID comparisons | 512 index entries / 64 lookups                                                    |
| Entries, C=1 B=512    |           0.1100 |            0.0923 |          1.19x |                   53,840 / 2,720 |           512 ID comparisons | 512 lookups / 512 linear comparisons                                              |
| v1 ignores, I=100     |           0.2889 |            0.0831 |          3.48x |                  15,032 / 18,712 |     3,725 ignore comparisons | 1 lookup / 50 index entries / 50 membership checks                                |
| v3 ignores, I=100     |           0.1900 |            0.1281 |          1.48x |                  15,032 / 29,352 |     3,725 ignore comparisons | 1 lookup / 50 index entries / 50 membership checks                                |
| v1 ignores, I=500     |           1.9256 |            0.2187 |          8.80x |                  54,904 / 79,136 |    93,625 ignore comparisons | 1 lookup / 250 index entries / 250 membership checks                              |
| v3 ignores, I=500     |           3.3220 |            0.4818 |          6.90x |                 54,904 / 130,880 |    93,625 ignore comparisons | 1 lookup / 250 index entries / 250 membership checks                              |
| v1 ignores, I=2,000   |          65.6254 |            1.1503 |         57.05x |                205,192 / 299,760 | 1,499,500 ignore comparisons | 1 lookup / 1,000 index entries / 1,000 membership checks                          |
| v3 ignores, I=2,000   |          89.6731 |            1.1712 |         76.59x |                205,192 / 492,176 | 1,499,500 ignore comparisons | 1 lookup / 1,000 index entries / 1,000 membership checks                          |

The effect contract passes: all outputs match, the deterministic index and membership work is
linear, both I=2,000 cases exceed the 2x timing target, both small cases are below the 2x slowdown
limit, and large-ignore allocations stay below 3x (1.46x for v1, 2.40x for v3). This run showed
the small v3 workload 25% slower and the C=512 contract-only workload allocating 3.68x as much,
while that contract lookup was 20.24x faster. These are synthetic microbenchmarks; the small
sub-millisecond timing differences are sensitive to host noise. Current forced-GC retained-output
deltas were 184 KB for both I=2,000 cases; that does not measure peak temporary memory. No process
peak working-set value was captured.

## Review follow-up: sparse and imported-only groups

The reviewer-requested shape matrix used C=512, early or late matching IDs, and imported-only
external-diagnostic groups. Each row is a five-sample alternating median on the same Windows/.NET
10 host. “Legacy” is the pre-index merge reference; “Current” is the lazy/single-ID path. The
legacy comparison count is the prior linear contract scan.

| Shape                        | B / imported diagnostics | Legacy ms / bytes | Current ms / bytes | Legacy/current | Legacy ID comparisons | Current contract work       |
| ---------------------------- | -----------------------: | ----------------: | -----------------: | -------------: | --------------------: | --------------------------- |
| First match, one entry       |                    1 / 0 |    0.0396 / 8,872 |     0.0284 / 6,872 |          1.39x |                     1 | 1 lookup / 1 comparison     |
| First match, repeated ID     |                    4 / 0 |    0.0438 / 9,160 |     0.0444 / 6,872 |          0.99x |                     4 | 4 lookups / 1 comparison    |
| Last match, one entry        |                    1 / 0 |    0.0593 / 8,872 |     0.1113 / 6,872 |          0.53x |                   512 | 1 lookup / 512 comparisons  |
| Last match, repeated ID      |                    4 / 0 |    0.1687 / 9,160 |     0.0481 / 6,872 |          3.51x |                 2,048 | 4 lookups / 512 comparisons |
| Imported-only external group |                   1 / 64 |    0.0387 / 8,864 |     0.0427 / 6,984 |          0.91x |                     0 | no index or lookup          |
| Imported-only external group |               64 / 4,096 |   0.2117 / 12,072 |    0.1447 / 10,128 |          1.46x |                     0 | no index or lookup          |

Every digest matched. Most timings in this matrix are below 0.2 ms and varied by shape; the late
single-entry result favored the legacy path, while repeating the same late ID four times favored
the cached current lookup. These micro-measurements do not establish a product regression. The
current path reuses one lookup when up to eight entries share a case-insensitive ID, keeps the
dictionary path for other multi-ID groups, and filters imported external diagnostics before index
creation. The imported-only matrix confirms that no contract lookup or index is created there.

## Alternating CLI A/B follow-up

The same generated `--timings` fixture was run in five alternating pairs, base/current then
current/base, comparing d1e43a53 with PR head 67715f2 before the current review fixes. Each command
used the same Release host and identical policy/baseline files. This earlier A/B sample was not
repeated for the current working changes.

|                                 Pair | Base total / `baseline_loading` ms | Revised total / `baseline_loading` ms |
| -----------------------------------: | ---------------------------------: | ------------------------------------: |
|                                    1 |                        1,376 / 400 |                           1,258 / 201 |
|                                    2 |                        1,264 / 207 |                           1,379 / 399 |
|                                    3 |                        1,402 / 414 |                           1,379 / 220 |
|                                    4 |                        1,430 / 323 |                           1,455 / 411 |
|                                    5 |                        1,399 / 407 |                           1,271 / 214 |
|                               Median |                        1,399 / 400 |                           1,379 / 220 |
| Median paired delta (revised - base) |                                  — |                            -23 / -193 |

Paired total deltas ranged from -128 to +115 ms. The composite phase alternated between roughly
200 and 400 ms across adjacent runs, so this small sample cannot establish an end-to-end regression
or product speedup. The phase still includes YAML decoding and content identity work, not merge
alone; no general CLI speedup is claimed from the isolated merge measurements.

## Reproduction

With restore assets present, run the direct matrix before or after the implementation:

```powershell
dotnet test tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj --no-restore -c Release `
  --filter FullyQualifiedName~RunBaselineMergeEffectMatrix `
  --logger 'console;verbosity=detailed'
```

The test is `[Explicit]` and stays out of routine test and acceptance runs. Its JSON rows include
the workload dimensions, current direct medians, allocations, retained deltas, deterministic legacy
scan counts, canonical digests, and environment. The sparse review shapes use a second explicit
matrix:

```powershell
dotnet test tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj --no-restore -c Release `
  --filter FullyQualifiedName~RunSparseAndImportedOnlyEffectMatrix `
  --logger 'console;verbosity=detailed'
```

Generate the CLI fixture with the script above. For a fresh checkout, make a detached worktree at
the original PR head and build its Release CLI, then build the current branch's Release CLI:

```powershell
git worktree add --detach ..\arch-linter-net-issue-1126-ab-base d1e43a53
dotnet build ..\arch-linter-net-issue-1126-ab-base\src\ArchLinterNet.Cli\ArchLinterNet.Cli.csproj -c Release
dotnet build src\ArchLinterNet.Cli\ArchLinterNet.Cli.csproj -c Release
```

Run an alternating A/B comparison with both Release CLI assemblies available:

```powershell
powershell -NoProfile -File docs/internal/large-solution-benchmarking/issue-1126-run-cli-ab.ps1 `
  -BaseCli ..\arch-linter-net-issue-1126-ab-base\src\ArchLinterNet.Cli\bin\Release\net10.0\ArchLinterNet.Cli.dll `
  -CurrentCli src\ArchLinterNet.Cli\bin\Release\net10.0\ArchLinterNet.Cli.dll
```

Remove the temporary worktree after collecting the measurements with
`git worktree remove ..\arch-linter-net-issue-1126-ab-base`.

## Validation

Commands run while fixing the review:

| Check                                                                                                     | Result                                                                                                                                                         |
| --------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `dotnet test ... --filter FullyQualifiedName~ArchitectureBaselineMergeIndexTests`                         | Passed, 21/21, including a direct first-match check for case-insensitive duplicate IDs in lazy and indexed paths.                                              |
| Baseline merge, loader, applicability, imported diagnostics, and federation tests                         | Passed, 91/91.                                                                                                                                                 |
| `dotnet test ... --filter FullyQualifiedName~ArchitectureBaseline --collect:"XPlat Code Coverage"`        | Passed, 136/136. New contract lookup lines were 100% covered; membership lines were 98.4% covered, with uncovered generated record-key members.                |
| Explicit performance matrices (`RunBaselineMergeEffectMatrix` and `RunSparseAndImportedOnlyEffectMatrix`) | Passed, both matrices; all ordered output digests matched. Current medians and deterministic work counts are above.                                            |
| `dotnet format ArchLinterNet.slnx --no-restore --verify-no-changes` on changed C# files                   | Passed.                                                                                                                                                        |
| `make lint-code-size`                                                                                     | Passed with existing size warnings; the baseline loading service is 754 lines and the expanded test fixture was split below 500 lines.                         |
| `make lint-architecture`                                                                                  | Passed strict self-policy validation; no cycles and project coverage complete.                                                                                 |
| `make public-api-check`                                                                                   | Passed for Core, Testing, and CEL snapshots.                                                                                                                   |
| `make lint-workflows`                                                                                     | Passed actionlint, zizmor, and Prettier checks.                                                                                                                |
| Qodana workflow contract tests under the repository Python 3.13 environment                               | Passed, 84/84.                                                                                                                                                 |
| `openspec validate --all`                                                                                 | Passed, 175/175 items. Existing repository-wide warnings remain non-fatal.                                                                                     |
| `git diff --check`                                                                                        | Passed.                                                                                                                                                        |
| `make lint-docs`                                                                                          | 39 passed, 4 failed: unchanged governance-review shell tests timed out after 30 seconds while invoking Git Bash on Windows. The MkDocs build step did not run. |

The full Core suite was not rerun during this fix; the earlier local attempt had eight self-policy
failures because Debug rebuilds could not overwrite the CLI DLL held by the testhost. CI remains the
authoritative full-suite check. `make lint-docs` also remains incomplete locally because of the
Windows shell-test timeouts described above. SonarCloud's new-code coverage gate and the trusted
Qodana finding gate must be confirmed by CI on the updated commit.
