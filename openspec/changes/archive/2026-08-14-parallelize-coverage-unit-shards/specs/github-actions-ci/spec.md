## MODIFIED Requirements

### Requirement: SonarCloud analysis consumes isolated coverage artifacts

SonarCloud analysis SHALL run inside `ci.yml`'s coverage/Sonar job. The coverage/Sonar job SHALL
consume the complete set of .NET coverage/TRX artifacts produced by the isolated coverage shard
jobs, rather than re-running the .NET unit suite serially. For trusted analyses, it SHALL still run
a build between `dotnet-sonarscanner begin` and `end` so Scanner for .NET receives the MSBuild and
Roslyn analysis data it requires.

#### Scenario: SonarCloud reuses coverage results without re-running .NET tests

- **WHEN** the coverage/Sonar job runs after all .NET coverage shards succeed
- **THEN** it downloads their Cobertura/OpenCover/TRX artifacts
- **AND** it does not execute the coverage-eligible .NET unit suite again
- **AND** a trusted run builds the solution inside the SonarCloud begin/end lifecycle before the
  reports are imported

#### Scenario: Pull request analysis has enough Git metadata

- **WHEN** SonarCloud analyzes a pull request
- **THEN** the coverage/Sonar job checks out the repository with full history
- **AND** SonarCloud receives enough branch comparison metadata to evaluate the pull request
  against its base branch

### Requirement: Pull request validation runs as independently schedulable jobs

`ci.yml` SHALL decompose pull request validation into independently schedulable jobs along real
dependency boundaries instead of one monolithic job, so unrelated checks can start concurrently
and fail independently. At minimum the workflow SHALL provide: a workflow-quality job (actionlint,
zizmor, workflow Prettier check), a repository-lint job (`make lint`), independently schedulable
.NET coverage shard jobs, a coverage/Sonar aggregation job (SonarCloud begin/build/end lifecycle,
Python tooling coverage, coverage artifact import, Codecov upload), an architecture-coverage job
(strict/audit architecture coverage analysis, artifact uploads, PR comment), and a
tooling/support-tests job (the architecture-coverage-report-generator and release-evidence-
aggregator Python test suites). `needs:` SHALL be used only along genuine artifact/result
boundaries; specifically the coverage/Sonar aggregation job MAY depend on the .NET coverage shard
jobs whose reports it consumes, while unrelated validation jobs remain schedulable at PR start.

#### Scenario: Workflow quality fails without waiting for .NET restore

- **WHEN** a pull request run reaches the workflow-quality job
- **THEN** that job runs actionlint, zizmor, and the workflow Prettier check
- **AND** it does not perform a .NET restore or depend on any job that does

#### Scenario: Repository lint does not wait on Sonar or coverage

- **WHEN** a pull request run reaches the repository-lint job
- **THEN** that job runs `make lint` (or an equivalent decomposition preserving every existing lint
  signal) using its own restore
- **AND** it has no `needs:` edge on the coverage/Sonar job

#### Scenario: Coverage/Sonar job excludes E2E and packed-artifact work

- **WHEN** the coverage/Sonar job runs
- **THEN** it consumes only coverage-eligible .NET test reports, runs Python tooling coverage,
  performs SonarCloud begin/build/end for trusted pull requests, and performs the Codecov upload
- **AND** it does not invoke the E2E or packed-artifact test buckets
- **AND** it does not re-run the .NET coverage shards whose reports it consumes

#### Scenario: Architecture coverage job builds what it needs independently

- **WHEN** the architecture-coverage job runs on its own runner and checkout
- **THEN** it builds the projects required by the strict/audit architecture coverage analysis
  itself, rather than relying on a build performed by a different job
- **AND** it preserves fail-closed strict-mode failure semantics even though artifact upload and
  PR comment steps use `always()`/`continue-on-error` for report publication

#### Scenario: No artificial ordering between unrelated validation jobs

- **WHEN** workflow-quality, repository-lint, .NET coverage shards, coverage/Sonar aggregation,
  architecture-coverage, and tooling/support-tests jobs are inspected
- **THEN** only the coverage/Sonar aggregation job depends on .NET coverage shards because it
  genuinely consumes their artifacts
- **AND** unrelated jobs declare no artificial `needs:` edges between each other

### Requirement: Coverage collection reuses deterministic Core shard boundaries on isolated runners

The authoritative CI coverage path SHALL split `ArchLinterNet.Core.Tests` using the same committed
`TEST_CORE_UNIT_SHARD_1_FILTER` / `TEST_CORE_UNIT_SHARD_2_FILTER` boundaries used by correctness
sharding, and SHALL collect the non-Core CEL/CLI unit assemblies separately. Each coverage shard
SHALL run in its own CI job/workspace so Coverlet never instruments and restores the same built
assembly concurrently from multiple processes. The downstream coverage/Sonar or main-branch
aggregation job SHALL consume the union of all produced coverage artifacts and SHALL fail closed
when any required coverage shard fails or produces no coverage artifact.

The local aggregate `make test-coverage` command MAY remain a single unsharded run because it runs
inside one checkout and is not the PR feedback critical path.

#### Scenario: Core coverage shards are independently schedulable and race-free

- **WHEN** pull-request or main-branch .NET coverage executes in CI
- **THEN** Core shard 1 and Core shard 2 run as independent Ubuntu jobs using their existing
  deterministic filters
- **AND** CEL/CLI coverage runs in a separately schedulable coverage job
- **AND** no two coverage shards instrument the same checkout's built assemblies concurrently

#### Scenario: Aggregated reports preserve complete coverage

- **WHEN** all .NET coverage shard jobs succeed
- **THEN** their Cobertura, OpenCover, and TRX artifacts are downloaded by the downstream
  aggregation job
- **AND** SonarCloud and Codecov receive the complete union of those reports
- **AND** the aggregation job does not execute the .NET unit tests again

#### Scenario: A failed coverage shard fails the authoritative aggregate signal

- **WHEN** any required .NET coverage shard fails or its artifact is unavailable
- **THEN** the downstream Coverage + Sonar or Main Badge Refresh job fails
- **AND** it does not report a successful aggregate coverage/Sonar result from a partial test set
