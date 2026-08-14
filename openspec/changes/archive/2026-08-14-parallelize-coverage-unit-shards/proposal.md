## Why

The Core unit suite was deterministically split for #478, but the authoritative .NET coverage path
was deliberately left as one unsharded `dotnet test --collect` invocation. That decision preserved
Coverlet correctness inside a single checkout, but it also preserved the largest serial CI
critical path: on the August 14 `main` run at `7cf469d`, `ArchLinterNet.Core.Tests` took about
11m36s under coverage, and PR #584 still took about 9m35s for the same assembly even though its
ordinary Core correctness jobs were already split into independent shards.

The old design correctly identified the shared-bin instrumentation race, but drew the wrong
conclusion for a repository whose parent CI story explicitly optimizes developer feedback latency
over hosted runner-minutes. Coverage does not need to remain monolithic; it needs to remain
isolated. Running the existing deterministic Core shards in separate GitHub-hosted workspaces
preserves Coverlet's instrumentation/restore safety while allowing the expensive coverage work to
happen concurrently.

Related: #474, #478.

## What Changes

- Add `test-coverage-core-1`, `test-coverage-core-2`, and `test-coverage-other` Make targets. The two
  Core targets reuse the exact existing shard filters; the non-Core target covers CEL.Tests and
  Cli.Tests sequentially.
- Keep local `make test-coverage` as a safe single-checkout aggregate command. Do not run multiple
  Coverlet collectors concurrently against the same `bin`/`obj` tree.
- Add a three-leg Ubuntu `.NET Coverage` matrix to `ci.yml`. Each leg owns an isolated checkout,
  collects Cobertura/OpenCover/TRX evidence, and uploads a required artifact.
- Make `Coverage + Sonar` and `Main Badge Refresh` explicit fan-in jobs over the coverage matrix.
  They fail closed if any coverage shard fails, download the complete report union, and do not
  execute the .NET unit suite again.
- Preserve Scanner for .NET integration by building the solution inside `dotnet-sonarscanner
  begin`/`end` on trusted analyses before importing the downloaded coverage reports.
- Keep E2E and packed-artifact work out of the coverage path; no test or coverage threshold is
  removed or weakened.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `github-actions-ci`: coverage collection now reuses the deterministic Core shard boundaries on
  isolated runners and feeds a fail-closed Sonar/Codecov aggregation stage.

## Impact

- `make/test.mk`: isolated CI coverage shard targets and an optional diagnostics argument.
- `.github/workflows/ci.yml`: three parallel coverage legs plus artifact fan-in to the existing
  reviewer-visible Coverage + Sonar signal and main badge refresh.
- `openspec/specs/github-actions-ci/spec.md`: replaces the obsolete single-unsharded-coverage
  requirement and records the real artifact dependency edge.
- No production C# code, policy semantics, supported platform correctness coverage, release gate,
  Codecov threshold, or Sonar quality-gate policy changes.
