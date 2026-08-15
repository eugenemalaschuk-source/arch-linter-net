## 1. Coverage entry points

- [x] 1.1 Add isolated `test-coverage-core-1` and `test-coverage-core-2` targets that reuse the
      existing deterministic Core shard filters.
- [x] 1.2 Add `test-coverage-other` for CEL.Tests and Cli.Tests, keeping those two fast assemblies
      sequential inside one isolated coverage workspace.
- [x] 1.3 Keep `make test-coverage` as the safe one-checkout aggregate command and document why the
      CI shard targets must not be run concurrently against one local `bin`/`obj` tree.
- [x] 1.4 Preserve optional main-push hang diagnostics through `TEST_COVERAGE_DIAGNOSTICS` rather
      than duplicating the shard target implementations.

## 2. CI topology

- [x] 2.1 Add a three-leg Ubuntu `.NET Coverage` matrix (Core shard 1, Core shard 2, CEL + CLI),
      each with its own checkout/restore/build/collector execution.
- [x] 2.2 Upload each shard's Cobertura/OpenCover/TRX results as a required artifact.
- [x] 2.3 Convert `Coverage + Sonar` into a fail-closed fan-in job over the coverage matrix; download
      all shard artifacts and remove the monolithic `make test-coverage` execution.
- [x] 2.4 Preserve Scanner for .NET's begin/build/end lifecycle by building the solution after
      `begin`, without re-running .NET unit tests.
- [x] 2.5 Apply the same coverage artifact fan-in to `Main Badge Refresh` and retain main-push hang
      diagnostics in the producer jobs.
- [x] 2.6 Keep Codecov and Sonar recursive report globs so the complete multi-artifact report union
      is imported without a lossy pre-merge step.

## 3. Fail-closed behavior

- [x] 3.1 Make coverage artifact upload fail when a shard produces no files.
- [x] 3.2 Run aggregate jobs with `always()` after their coverage dependency so a failed matrix is
      surfaced as a red aggregate signal rather than a silently missing/skipped one.
- [x] 3.3 Fail the aggregate before Sonar/Codecov when the coverage matrix result is non-success.
- [x] 3.4 Fail report resolution when no Cobertura report exists after artifact download.

## 4. Specification synchronization

- [x] 4.1 Replace the obsolete requirement that coverage remain one unsharded process with an
      isolation requirement: same deterministic Core shards, separate CI workspaces, complete
      downstream artifact union.
- [x] 4.2 Update the SonarCloud requirement to consume coverage artifacts and perform a Sonar build
      rather than re-running coverage tests.
- [x] 4.3 Record the coverage-shard -> aggregate `needs:` edge as a genuine artifact dependency,
      while preserving parallel scheduling for unrelated validation jobs.

## 5. Validation

- [x] 5.1 Inspect the existing PR/main coverage logs and establish the baseline: Core coverage
      9m35s on PR #584 and 11m36s on its base/main run; Sonar post-processing is not the dominant
      cost.
- [x] 5.2 Verify the existing normal unit shards still cover the Core suite independently and that
      the new coverage targets reuse those exact filter definitions.
- [x] 5.3 Verify the workflow keeps E2E and packed-artifact tests outside the coverage path and does
      not weaken coverage or quality-gate policy.
- [x] 5.4 Delegate workflow syntax/security checks, the real multi-runner coverage execution,
      Sonar report import, Codecov upload, and timing evidence to the PR's authoritative CI per the
      repository's risk-based local-validation contract.
