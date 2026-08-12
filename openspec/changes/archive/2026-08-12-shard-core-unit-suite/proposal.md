## Why

`ArchLinterNet.Core.Tests` has no assembly-level NUnit parallelism, so it runs strictly serially
and its ~365s local wall-clock (~9-15m on hosted CI runners per #475/#481 evidence) dominates the
`unit_tests` job even after #475 isolated E2E and packed-artifact work onto their own jobs. Moving
E2E elsewhere was necessary but insufficient: the Core unit assembly is itself the remaining
critical path, and every authoritative PR must still run its complete unit suite. The fix is to
distribute that fixed amount of work across independent CI runners, not to run fewer tests.

## What Changes

- Add a duration-based, deterministic 2-shard partition of `ArchLinterNet.Core.Tests`, defined as
  explicit `FullyQualifiedName~<FixtureClass>` filter tokens in `make/test.mk`, following the
  exact single-sourced-filter-fragment pattern `TEST_E2E_FIXTURES`/`TEST_PACKED_ARTIFACT_FILTER`
  already establish. Shard 1 is a pure-OR list of the ~16 measured/categorically heaviest fixture
  classes (Roslyn/IL method-body resolution, framework-reference project resolution, filesystem/
  build-preservation, reflection-heavy checkers). Shard 2 is the remainder (`TEST_UNIT_FILTER` AND
  NOT each shard-1 token) so a newly added fixture is fail-closed into shard 2 by construction,
  never silently dropped.
- Add `test-unit-core-1`, `test-unit-core-2` (scoped to the `ArchLinterNet.Core.Tests` project,
  not the `.slnx`, so `ArchLinterNet.CEL.Tests`/`ArchLinterNet.Cli.Tests` cannot leak into both
  shards via filter negation) and `test-unit-other` (the CEL/Cli projects, neither of which
  contains any E2E/packed-artifact fixture) as stable Make targets. Redefine `test-unit` as the
  parallel union of all three, mirroring the existing `test:` target's parallel/wait-all pattern,
  so the aggregate command can never drift out of sync with the shards.
- Add a mechanical shard-membership validation (`tools/scripts/verify_core_unit_shards.py`,
  wired into `make lint` as `lint-test-shard-membership`) that parses the shard/E2E/packed-artifact
  filter tokens straight out of `make/test.mk` and cross-checks them against a live
  `dotnet vstest --ListFullyQualifiedTests` discovery dump: fails on a dead shard-1 token (matches
  zero discovered tests) or a token collision with an E2E/packed-artifact fixture (leak), and
  reports the resulting partition counts. Runs in the existing `repository_lint` CI job - no new
  workflow file.
- `.github/workflows/ci.yml`'s `unit_tests` job matrix gains a `shard: [1, 2]` axis crossed with
  the existing `os` axis (4 legs total), each leg running `make test-unit-core-${{ matrix.shard }}`;
  shard-1 legs additionally run `make test-unit-other`.
- `make test-coverage`/`test-coverage-main-ci` are unchanged - coverage stays a single unsharded
  run of the full `TEST_UNIT_FILTER` set, avoiding the shared-bin/torn-read coverage races already
  documented in `make/test.mk`.
- Record the measured fixture-duration inventory and shard rationale in a new internal doc so
  future maintainers understand why the partition exists and how to extend it.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `github-actions-ci`: adds a requirement that the `unit_tests` job runs the Core unit suite as a
  deterministic, duration-based multi-shard matrix (rather than one combined bucket per platform),
  with a fail-closed mechanical check that every discovered Core unit test belongs to exactly one
  shard and no E2E/packed-artifact test leaks into a unit shard.

## Impact

- `make/test.mk`: new shard filter variables, `test-unit-core-1`/`test-unit-core-2`/
  `test-unit-other` targets, `test-unit` redefined as their parallel union,
  `lint-test-shard-membership` target.
- `make/lint.mk`: `lint-test-shard-membership` added to the `lint` aggregate target.
- `tools/scripts/verify_core_unit_shards.py` (new) + a lean pytest test.
- `.github/workflows/ci.yml`: `unit_tests` job matrix restructured with a `shard` axis.
- `docs/internal/`: new fixture-duration-inventory doc, indexed from `docs/internal/README.md`.
- No production code, architecture-governed assemblies, or coverage/Sonar quality policy changes.
