## Context

`ArchLinterNet.Core.Tests` is already split into two deterministic `FullyQualifiedName` shards for
ordinary cross-platform correctness CI. Coverage remained deliberately unsharded because Coverlet's
VSTest collector instruments assemblies in the checkout and restores them after execution; two
coverage collectors sharing one `bin`/`obj` tree could therefore observe or overwrite each
other's instrumented files.

That safety constraint is real, but it is a workspace-isolation constraint rather than a reason to
serialize the entire coverage suite. Recent hosted-runner evidence shows the cost of preserving the
old topology: Core alone takes roughly 9-12 minutes under coverage while the non-coverage shard
jobs finish far sooner. The parent story #474 explicitly prefers shorter wall-clock feedback even
when it costs more hosted runner-minutes.

## Goals / Non-Goals

**Goals:**
- Reuse the exact existing Core shard membership for coverage so correctness and coverage cannot
  develop independent partition definitions.
- Execute the expensive Core coverage halves concurrently without ever sharing instrumented build
  outputs.
- Preserve the complete Cobertura/OpenCover/TRX report union consumed by SonarCloud and Codecov.
- Fail closed when a coverage shard fails or does not publish its artifact.
- Preserve Scanner for .NET's begin/build/end lifecycle and PR decoration/quality-gate behavior.

**Non-Goals:**
- Parallel Coverlet processes in one checkout.
- New test selection, fewer tests, or probabilistic coverage.
- Changing the Core shard membership itself.
- Changing cross-platform correctness matrices, E2E/packed-artifact topology, coverage thresholds,
  or Sonar quality policy.
- Making local `make test-coverage` parallel; local safety and simplicity are more important than
  local coverage latency here.

## Decisions

### Coverage uses the existing Core shard filters

`test-coverage-core-1` and `test-coverage-core-2` call the same
`TEST_CORE_UNIT_SHARD_1_FILTER` / `TEST_CORE_UNIT_SHARD_2_FILTER` already used by ordinary unit
shards. Shard 2 remains the fail-closed remainder. This preserves the already-mechanically-checked
union property instead of introducing a second coverage-specific partition.

CEL.Tests and Cli.Tests are already fast and do not need further partitioning. They run sequentially
inside one `test-coverage-other` target so only three coverage runners are required.

### Isolation is provided by CI jobs, not local processes

Each `.NET Coverage` matrix leg receives its own GitHub-hosted runner and checkout. Coverlet may
instrument and restore files inside that workspace without another collector touching the same
files. This directly resolves the race that motivated the old unsharded-coverage decision.

The root `test-coverage` command deliberately remains one solution-scoped collector run. The new
shard targets are CI building blocks; running them concurrently from one local checkout is outside
the supported contract.

### Coverage reports are artifacts; Coverage + Sonar is a fan-in job

Every coverage leg uploads `test-results/coverage` as an artifact named by shard. The downstream
aggregation job uses `actions/download-artifact` with the `dotnet-coverage-*` pattern and imports
all `coverage.cobertura.xml`, `coverage.opencover.xml`, and `.trx` files via recursive globs already
used by Sonar/Codecov.

`Coverage + Sonar` declares the only new intentional `needs:` edge in PR validation because it
actually consumes those artifacts. `Main Badge Refresh` uses the same dependency on main pushes.
Both use `always()` so they can emit a deterministic failure when the coverage matrix failed,
rather than being silently skipped and leaving a required aggregate check missing.

### Sonar builds, but does not re-run .NET tests

Scanner for .NET needs an MSBuild execution between `begin` and `end` to generate Roslyn analysis
metadata. Coverage files generated before/on other runners are nevertheless ordinary OpenCover/TRX
inputs and can be imported by the `end` phase. Therefore trusted aggregation jobs perform
`dotnet build ArchLinterNet.slnx --no-restore --nologo` after `begin`, but they do not execute any
.NET tests.

This keeps the reviewer-facing `Coverage + Sonar` job as the Sonar quality-gate signal without
putting the 9-12 minute serial unit execution back on its critical path.

### Coverage artifact completeness is fail-closed

A failed matrix leg makes `needs.dotnet_coverage.result` non-success. The aggregation job checks
that result first and exits non-zero. Artifact upload itself uses `if-no-files-found: error`, and the
aggregation job also errors if no Cobertura report is discoverable after download. Thus a partial
coverage set cannot be presented as a successful aggregate.

## Risks / Trade-offs

- **More runner-minutes:** each coverage shard restores/builds independently. This is accepted by
  #474's wall-clock-first CI strategy.
- **Artifact path portability:** all coverage producers and consumers use Ubuntu hosted runners and
  the same repository/workspace layout, while report discovery is recursive. If GitHub changes the
  hosted workspace root materially, CI evidence will expose the import failure rather than silently
  succeeding because report presence and Sonar quality gate are both checked.
- **Shard imbalance under coverage:** the existing shards were tuned for correctness timing rather
  than Coverlet overhead. Even an imperfect split removes the full serial Core process from the
  aggregate critical path; real PR timings should drive any future rebalance, without changing the
  isolation design.
- **Additional required-looking checks:** the new `.NET Coverage (...)` jobs do not need a manual
  ruleset update for correctness because the existing `Coverage + Sonar` aggregate fails when any
  dependency fails. They remain independently visible for diagnosis.

## Migration Plan

No persisted-state migration. The workflow change is atomic with the new Make targets and spec:
coverage shard jobs appear, aggregate jobs consume them, and the old monolithic CI coverage command
is no longer invoked. A plain revert restores the previous topology.

## Open Questions

None. The first PR run is the authoritative validation of report import behavior and real shard
wall-clock timing.
