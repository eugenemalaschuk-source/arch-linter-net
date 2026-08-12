# Core unit suite shard inventory

Baseline evidence and rationale for the `ArchLinterNet.Core.Tests` shard partition introduced for
#478 (parent story #474, following #475's unit/E2E/packed-artifact bucket split and #477's PR
validation job split). Kept for future maintainers deciding whether/how to rebalance the shards.

## Why Core.Tests specifically

Inside the unit bucket (`make test-unit`), `ArchLinterNet.Core.Tests` is the dominant cost:

| Assembly | Tests | Local wall-clock | Notes |
| --- | --- | --- | --- |
| `ArchLinterNet.CEL.Tests` | 584 | ~4s | negligible |
| `ArchLinterNet.Cli.Tests` | 489 | ~22-27s | `[assembly: Parallelizable(ParallelScope.All)]` + `LevelOfParallelism(8)` already |
| `ArchLinterNet.Core.Tests` | 2571 | ~365s local (~9-15m on CI per #475/#481 evidence) | **no assembly-level NUnit parallelism** - runs strictly serially |

Because `Core.Tests` has no `[assembly: Parallelizable]`, its wall-clock is close to the sum of its
own fixture costs, which makes a duration-ranked partition of its fixture classes directly
meaningful (unlike `Cli.Tests`, where NUnit's own internal parallelism already hides most of the
serial cost).

## Methodology

1. `dotnet build ArchLinterNet.slnx --no-restore --nologo` (build once).
1. `dotnet test ArchLinterNet.slnx --no-restore --no-build --filter "$TEST_UNIT_FILTER" --logger trx --results-directory <dir>`
   run in isolation (no other CPU-heavy process concurrent with it - see the anomaly note below).
1. Per-fixture cost was read from the TRX `<UnitTestResult duration="...">` attribute, which NUnit
   records per test result and excludes `[OneTimeSetUp]`/`[OneTimeTearDown]` cost. This
   under-counts fixtures with expensive one-time setup, but proved far less noisy locally than
   attributing wall-clock gaps between consecutive result timestamps (see below), so it is the
   primary signal used to rank fixtures.
1. Fully-qualified test discovery for validating shard-token uniqueness used
   `dotnet vstest tests/ArchLinterNet.Core.Tests/bin/Debug/net10.0/ArchLinterNet.Core.Tests.dll --ListFullyQualifiedTests --ListTestsTargetPath:<file>`.
   **`dotnet test --list-tests` silently ignores `--filter`** and cannot be used for this - it was
   tried first and returned the full unfiltered discovery list regardless of filter, which is why
   `dotnet vstest` against the built DLL directly is what `tools/scripts/verify_core_unit_shards.py`
   uses too.

### A local-environment anomaly, and why it doesn't drive the partition

An earlier wall-clock-gap-based attribution attempt found a single ~200s gap that reproducibly
followed the `Checkers.*` fixture group (`PublicApiSurfaceCheckerTests`,
`InheritanceCheckerTests`, `AssemblyIndependenceCheckerTests`) regardless of which fixtures ran
before or after it in two separately-composed runs. Removing an unrelated large fixture from the
run (`CompositionContractTests`) changed almost nothing about total wall-clock, which ruled out
"cost of whichever fixture happens to run next" as the explanation. The `Checkers.*` classes'
**own** TRX-recorded durations are trivial (≤0.05s each), so this cost is not attributable to test
body execution - it is most plausibly a one-time local-machine cost (e.g. antivirus/indexer
scanning freshly-written assemblies, or a JIT tiering/GC event tied to this machine's specific
state) rather than a real per-fixture cost that would reproduce on hosted CI runners. It is
recorded here rather than silently discarded, and the `Checkers.*` classes were still placed in
shard 1 defensively (see below) so real CI evidence - not local noise - is what should decide
whether they need to move.

**Local absolute numbers are not CI-authoritative.** `make test-unit`'s local wall-clock (~365s
total, ~140s of which is TRX-recorded `Core.Tests` fixture duration) is far smaller than the
~9-15 minute unsharded Windows/Intel-macOS `unit_tests` job durations recorded in #481's PR
evidence for the same bucket. The *ranking* of which fixture families are expensive is still
useful signal (and matches the issue's own guidance about which categories cost more than raw test
count implies); the PR for #478 records real per-shard CI timings as the actual acceptance
evidence.

## Ranked fixture-duration inventory (top 15 of 228 classes)

| Rank | Class | Duration | Tests | Category |
| --- | --- | --- | --- | --- |
| 1 | `PerTestDurationGuardAttributeTests` | 16.01s | 6 | intentional timing/guard integration test |
| 2 | `EnsureBuiltNonDestructiveIntegrationTests` | 14.21s | 2 | filesystem/build preservation |
| 3 | `FrameworkReferenceContractTests` | 14.08s | 8 | project/framework resolution |
| 4 | `FrameworkReferenceConfigurationTests` | 13.76s | 8 | project/framework resolution |
| 5 | `FrameworkReferenceBaselineIdentityTests` | 8.62s | 9 | project/framework resolution |
| 6 | `ArchitectureAnalysisSessionMethodBodyProjectAwareTests` | 8.58s | 7 | Roslyn/IL method-body resolution |
| 7 | `FrameworkReferenceAllowOnlyContractTests` | 7.40s | 5 | project/framework resolution |
| 8 | `ArchitectureProjectRoslynContextResolverTests` | 5.75s | 6 | Roslyn project context |
| 9 | `AspNetSharedFrameworkAcceptanceTests` | 5.22s | 4 | filesystem/build, shared-framework acceptance |
| 10 | `BoundedParallelPartitionRunnerTests` | 5.15s | 12 | synthetic-project/process concurrency |
| 11 | `CompositionContractTests` | 4.65s | 26 | assembly/IL scanning |
| 12 | `AcyclicSiblingContractTests` | 2.52s | 30 | file/namespace scanning |
| 13 | `AnalysisCacheHmacKeyStoreTests` | 1.87s | 7 | filesystem/cache |
| 14 | `TransitiveReferenceScannerTests` | 1.78s | 5 | IL/reference scanning |
| 15 | `ExternalAllowOnlyContractTests` | 1.60s | 17 | assembly scanning |

The top 11 classes above account for ~74% of the assembly's ~140s summed TRX duration despite
being under 5% of its 228 fixture classes; the remaining 217+ classes are lightweight in-memory
contract-evaluation tests, mostly well under 1s each.

## Shard assignment

Shard 1 starts from the 11 measured-heavy classes above, plus `CelBoundaryArchitectureTests` and
the three `Checkers.*` classes (categorically reflection-heavy, included defensively per the
anomaly note above) - 16 tokens, ~131 of 2571 unit tests (~5%). **This alone was not balanced in
practice**, even though it looked balanced by summed TRX duration: running it as its own shard
took ~105-135s locally, and the ~2440-test remainder shard took ~346s - nowhere near 50/50, and
nowhere near the issue's 60%-of-baseline target. With that many tests left in one process,
per-test/per-process framework overhead invisible to any single test's recorded `<duration>`
dominates the real wall-clock far more than the visible "heavy fixture" costs do.

To correct this, 38 further classes were added to shard 1 - picked by discovered test *count*
(largest classes first, greedy, via `tools/scripts/verify_core_unit_shards.py`'s discovery output)
rather than measured per-test duration, until shard 1 reached roughly half of the bucket's 2571
tests: **1290 shard 1 / 1281 shard 2** (54 tokens total). This is a stable, machine-independent
balance metric - test count doesn't vary run to run, unlike local wall-clock.

**A second empirical check after rebalancing produced an unexpected result**: shard 1 (1288
executed tests) took ~120s, and shard 2 (1279 executed tests) took only ~26s - a large imbalance
in the *opposite* direction from the first attempt, despite the two shards now being almost
exactly count-balanced. Taken together with the first attempt's result and the `Checkers.*`
gap-attribution anomaly documented above, this is now the **third** independent case of local
wall-clock measurements swinging by a large factor between structurally similar runs on this
machine. The count-based partition is kept because it rests on a metric (discovered test count)
that is reproducible and independent of local timing noise, and because it still incorporates
every category the issue calls out as disproportionately expensive (Roslyn/IL, filesystem/
process, project-resolution, reflection-heavy) - but **local wall-clock numbers should not be
trusted to judge shard balance on this codebase**. The PR opened for #478 records real per-shard,
per-platform CI timings from its own run as the actual acceptance evidence, per the issue's own
validation requirement ("record real PR timings on all supported platforms").

Shard 2 is expressed as a negation of shard 1's 54 tokens against `TEST_UNIT_FILTER`, not as an
explicit list, so a newly added fixture is automatically covered without a manual assignment step
(see `make/test.mk` for the exact filter definitions and `tools/scripts/verify_core_unit_shards.py`
for the mechanical check that keeps this fail-closed).

To rebalance later: re-run `tools/scripts/verify_core_unit_shards.py` to get current discovered
counts per bucket, and move tokens between the explicit shard-1 list and the implicit remainder to
keep discovered test count roughly balanced. Cross-check against real CI job timings (not local
runs) before concluding a rebalance actually improved anything - this file's own history is the
cautionary example.
