## Context

`make/test.mk` already single-sources three VSTest `FullyQualifiedName` filter fragments
(`TEST_UNIT_FILTER`, `TEST_E2E_FILTER`, `TEST_PACKED_ARTIFACT_FILTER`) consumed by both local
`make test-*` targets and the `unit_tests`/`e2e_tests`/`packed_artifact_tests` CI matrix jobs
(#475). `ArchLinterNet.Core.Tests` carries 228 fixture classes / ~2571 unit tests with no
assembly-level NUnit parallelism (unlike `ArchLinterNet.Cli.Tests`, which runs
`[assembly: Parallelizable(ParallelScope.All)]` + `LevelOfParallelism(8)`), so its wall-clock is
close to the sum of its own fixture costs and it is the dominant cost inside the `unit_tests` job.

Two mechanism constraints were confirmed by direct experiment before choosing the approach below:

1. `dotnet test --list-tests` silently ignores `--filter` (returns the full unfiltered discovery
   list regardless of what filter is passed), so it cannot validate shard membership.
   `dotnet vstest <built-dll> --ListFullyQualifiedTests --ListTestsTargetPath:<file>` does honor
   scoping to a single assembly and returns authoritative fully-qualified test names without
   executing anything (~10s for `ArchLinterNet.Core.Tests.dll`).
2. Parenthesized VSTest filters (`X&(A|B)`) are silently mis-parsed by the NUnit3TestAdapter's
   fallback filter parser and end up matching the *entire* suite with no diagnostic - the same
   class of hazard already documented for control characters in `make/test.mk` (issue #480). Every
   filter fragment in this change stays in the flat `A|B|C` / `X&!~A&!~B` shapes the file already
   uses; no parentheses anywhere.

A fresh local `make test-unit` TRX baseline was captured to rank fixture cost (methodology and
full ranking recorded in the new `docs/internal/core-unit-shard-inventory.md`). The top ~11
fixture classes by measured NUnit-recorded `<duration>` account for ~74% of the assembly's summed
duration despite being under 5% of its fixture classes - confirming the issue's own guidance that
Roslyn/IL, filesystem/process, and project-resolution-heavy fixtures cost far more than their test
count implies, and making a duration-ranked explicit-token partition meaningful.

## Goals / Non-Goals

**Goals:**
- Deterministically assign every `ArchLinterNet.Core.Tests` unit test to exactly one of two
  shards, with a newly added test landing in shard 2 (the remainder) by construction rather than
  being silently dropped from CI.
- Keep the existing three-bucket (unit/E2E/packed-artifact) topology from #475 and the five
  independently-schedulable PR-validation jobs from #477 unchanged; only `unit_tests` gains a
  shard axis.
- Add a mechanical, fail-closed check that a shard-1 token cannot silently go dead (typo, renamed
  fixture) or silently collide with an E2E/packed-artifact fixture (leak).
- Keep coverage collection correctness-first: no change to `test-coverage`/`test-coverage-main-ci`.

**Non-Goals:**
- Physical test-project decomposition. The issue's own preferred-order ranks this first only "when
  groups have a real responsibility boundary and can move without creating production coupling."
  228 fixture classes in one project share internal test infrastructure (`FakeArchitectureFileSystem`,
  `TestPolicies`, shared fixture types); splitting them into separate assemblies is a large,
  separately-scoped refactor with real coupling risk, not a 2-4h CI-topology task. Explicitly
  deferred, matching the issue's own fallback ("otherwise a versioned explicit shard manifest/
  filter definition").
- Reducing platform coverage, weakening coverage thresholds, or removing/rewriting any test.
- Sharding `ArchLinterNet.CEL.Tests` or `ArchLinterNet.Cli.Tests` - both are already fast (~4s and
  ~22-27s respectively) and `Cli.Tests` is already internally parallel; neither is the bottleneck.
- More than two shards. Two roughly-balanced shards already comfortably clears the issue's 60%-of-
  baseline target (each shard's dominant tokens sum to roughly half the measured total); a third
  shard would add CI job overhead for a marginal per-shard latency gain the estimate doesn't
  justify. Revisit if `ArchLinterNet.Core.Tests` grows substantially.
- Changing the coverage/Sonar job topology - out of scope for this issue, owned by #474/#477.

## Decisions

### Shard filters as explicit fixture-class tokens in `make/test.mk` (not a separate manifest file)

`make/test.mk` already documents itself as the single source for bucket filter fragments ("Three
independently addressable test buckets, single-sourced here as VSTest FullyQualifiedName
filters"). Adding `TEST_CORE_UNIT_SHARD_1_FIXTURES` alongside `TEST_E2E_FIXTURES` in the same file,
in the same `A|B|C` shape, keeps one format and one location for every bucket/shard definition
instead of introducing a second manifest format (e.g. JSON) that Make and Python would both have
to parse and that could drift from the Makefile. The membership-check script parses these
variables directly out of `make/test.mk` with a small regex over `FullyQualifiedName~X` fragments,
so there is exactly one authored list, not two synchronized ones.

**Alternative considered**: a `shard-manifest.json` consumed by both Make (via a `jq`/Python
helper) and the validator. Rejected: adds a build-time dependency and a second file to keep in
sync with `make/test.mk` for no behavioral benefit at this scale (16 tokens).

### Shard 1 = pure OR of explicit heavy-fixture tokens; Shard 2 = remainder via negation

Mirrors the existing `TEST_E2E_FILTER` (pure OR) / `TEST_UNIT_FILTER` (AND-of-negations) shapes
exactly. Shard 2's filter is `TEST_UNIT_FILTER` (already excludes E2E/packed-artifact) with one
additional `&FullyQualifiedName!~<token>` per shard-1 token. This is the fail-closed property the
issue asks for: a new fixture class that nobody explicitly assigns automatically satisfies every
`!~` negation and lands in shard 2, so it is never unassigned - at worst it's mis-sized into the
"everything else" shard until someone deliberately promotes it to shard 1.

### Shard filters run against the `ArchLinterNet.Core.Tests` project directly, not the `.slnx`

`make test-unit` today runs `dotnet test $(SLNX) --filter $(TEST_UNIT_FILTER)`, which applies the
same textual filter to all three unit-eligible projects (`CEL.Tests`, `Cli.Tests`, `Core.Tests`).
Because `TEST_UNIT_FILTER`'s negations are class-name substrings that don't exist in
`CEL.Tests`/`Cli.Tests`, those assemblies pass the filter vacuously and run in full inside
`test-unit` today. A shard-2 filter built the same way (`TEST_UNIT_FILTER` + negations) would have
the identical problem *for both shards*: `CEL.Tests`/`Cli.Tests` tests satisfy every negation
regardless of which shard's tokens are negated, so they'd run in both `test-unit-core-1` and
`test-unit-core-2` - violating "no test in more than one shard." Scoping `test-unit-core-1`/
`test-unit-core-2` to `tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj` specifically
removes the ambiguity structurally instead of adding more negations to chase it. `CEL.Tests`/
`Cli.Tests` get their own `test-unit-other` target (unfiltered - neither project contains any
E2E/packed-artifact fixture, confirmed during exploration).

### `test-unit` redefined as the parallel union of the three new targets

Rather than leaving `test-unit` as an independent `dotnet test $(SLNX)`-based definition that could
silently drift from the shard filters over time, `test-unit` becomes
`test-unit-core-1 & test-unit-core-2 & test-unit-other`, run in parallel and waited on the same way
`test:` already waits on its three bucket processes (no `set -e`, explicit exit-code check after
all three finish, so one early failure doesn't orphan the others). This keeps `make test-unit` as
the single authoritative "whole unit bucket" command with no second definition to maintain.

### Coverage stays unsharded

The issue explicitly allows "keeping coverage collection in one coverage job while sharding
non-coverage cross-platform unit correctness jobs" when shared build outputs make per-shard
coverage collection risky. `make/test.mk` already documents shared-bin/torn-read hazards from
concurrent `dotnet test` processes against the same `obj`/`bin` output. Splitting coverage
collection into two `--collect` processes would reintroduce exactly that risk for a job that
already isn't the bottleneck being fixed here (coverage's own critical path is bounded by Sonar's
quality-gate wait, not by the unit run itself, per #475's PR evidence). `test-coverage`/
`test-coverage-main-ci` keep using `TEST_UNIT_FILTER` against the `.slnx`, unchanged.

### Mechanical membership check parses `make/test.mk` + live VSTest discovery, wired into `make lint`

A Python script under `tools/scripts/` (matching `lint_csharp_file_size.py`/
`test_coverage_badge.py` conventions) builds the Core.Tests project itself, runs
`dotnet vstest <dll> --ListFullyQualifiedTests`, and classifies every discovered FQN against the
token lists parsed from `make/test.mk`. It fails when:
- a shard-1 token matches zero discovered tests (dead/typo'd token - the shard would silently do
  less than intended without any test result ever failing);
- a shard-1 token's substring match also matches an E2E or packed-artifact fixture FQN (leak - the
  discovered VSTest semantics are literal substring match, confirmed collision-free for the chosen
  16-token set during exploration, but a future addition could reintroduce one).
It reports the resulting shard-1/shard-2/E2E/packed-artifact partition counts on success. Wiring it
into `make lint` (as `lint-test-shard-membership`) means it runs in the existing `repository_lint`
CI job with no new workflow file, matching #477's own "independently schedulable jobs along real
dependency boundaries" pattern - lint is exactly where a static-analysis-style check like this
belongs, not inside the test-execution jobs themselves.

**Alternative considered**: run the check as a step inside each `unit_tests` matrix leg. Rejected:
would run the same check redundantly on 4 legs × 2 platforms instead of once, and ties a static
consistency check to test-execution success/failure instead of lint success/failure.

### CI: `unit_tests` gains a `shard` matrix axis, explicit 4-entry `include`

Matches the existing `unit_tests`/`e2e_tests`/`packed_artifact_tests` style of a fully-enumerated
`matrix.include` list (rather than a computed `os` × `shard` cross-product with a derived `name`),
since the repository consistently prefers explicit lists over computed matrix values elsewhere in
`ci.yml`. Shard-1 legs run `make test-unit-core-1` then `make test-unit-other`; shard-2 legs run
only `make test-unit-core-2`.

## Risks / Trade-offs

- **[Risk] Local timing measurements don't transfully translate to hosted CI runners** (confirmed
  during exploration: local wall-clock ~365s vs. CI's documented ~9-15m for the same unsharded
  bucket) → **Mitigation**: the shard token selection combines measured local ranking with
  category reasoning (Roslyn/IL, filesystem/process, project-resolution families the issue itself
  calls out as disproportionately expensive), not raw local seconds alone, so the split should
  transfer even though absolute numbers won't. The PR records real per-leg CI timings from its own
  run as the actual acceptance evidence, matching the issue's own validation requirement.
- **[Risk] Shard imbalance if the heavy-fixture assumption is wrong for CI hardware** → **Mitigation**:
  the fail-closed remainder design means an imbalanced split is a latency regression to fix in a
  follow-up token rebalance, never a correctness or coverage gap - shard 2 still runs every test
  shard 1 doesn't.
- **[Risk] A future contributor adds a new heavy fixture and doesn't know to add it to shard 1** →
  **Mitigation**: it still runs (in shard 2, per the fail-closed remainder), so correctness is
  never at risk; only latency balance degrades gradually, and the inventory doc explains how to
  rebalance.
- **[Trade-off] More CI runner-minutes for lower wall-clock latency** - explicitly endorsed by the
  parent story (#474): "optimize primarily for wall-clock developer feedback latency, not for
  minimizing total hosted-runner minutes" for this public repository.

## Migration Plan

No data migration. Rollout is the PR merge itself:
1. Land `make/test.mk` shard targets + membership check + `ci.yml` matrix change together (they're
   only meaningful in combination).
2. First PR run on the branch itself provides real per-leg CI timing evidence.
3. Rollback is a plain revert - no state to unwind, no schema, no persisted artifact format.

## Open Questions

None - scope, mechanism, and validation are fully determined by the constraints discovered during
exploration (parenthesized-filter hazard, `--list-tests` ignoring `--filter`, cross-assembly leak
risk of solution-scoped filters).
