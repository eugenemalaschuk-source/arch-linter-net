# GitHub Actions CI Specification

## Purpose
Defines the GitHub Actions workflows for pull request validation and the separated CI/release pipeline.
## Requirements
### Requirement: Pull request validation workflow
ArchLinterNet SHALL provide a GitHub Actions CI workflow that validates pull requests and pushes with the repository acceptance gate and SonarCloud analysis without producing official release packages.

#### Scenario: Pull request validation runs with SonarCloud analysis
- **WHEN** a pull request targets the repository
- **THEN** the CI workflow restores packages and runs `make acceptance`
- **AND** it runs SonarCloud pull-request analysis for trusted repository pull requests

#### Scenario: Main branch push validation runs with SonarCloud analysis
- **WHEN** code is pushed to the `main` branch
- **THEN** the CI workflow restores packages and runs `make acceptance`
- **AND** it runs SonarCloud branch analysis for `main`

### Requirement: CI release separation
The CI workflow SHALL NOT perform official release packaging, publication, tagging, or GitHub Release creation.

#### Scenario: CI does not pack release packages
- **WHEN** the CI workflow runs for a pull request or push
- **THEN** it does not run `dotnet pack` for official versioned package artifacts

#### Scenario: CI does not use publishing identity
- **WHEN** the CI workflow runs for a pull request or push
- **THEN** it does not request publishing identity tokens or package publication credentials

#### Scenario: CI does not publish or release
- **WHEN** the CI workflow runs for a pull request or push
- **THEN** it does not publish packages, create tags, or create GitHub Releases

### Requirement: README quality signal badge
The repository README SHALL display the CI status badge, the dynamic Codecov
coverage badge, a dynamic ArchLinterNet architecture-policy badge sourced from
the dedicated strict-self-policy workflow on `main`, and live SonarCloud quality
badges. It SHALL explain that the architecture-policy badge proves a strict
self-policy pass, while architecture coverage and test coverage remain separate
signals.

#### Scenario: Quality badges and explanation are present
- **WHEN** a reader views the README
- **THEN** it shows a CI status badge sourced from `ci.yml`
- **AND** it keeps the dynamic Codecov coverage badge
- **AND** it shows a dynamic architecture-policy badge sourced from the
  dedicated workflow's `main` status
- **AND** it shows live SonarCloud badges for the configured SonarCloud project
- **AND** it links to documentation explaining the distinct CI, architecture
  policy, architecture coverage, and SonarCloud quality signals

### Requirement: Architecture coverage analysis runs in the existing CI workflow

The architecture coverage analysis steps (strict/audit JSON artifacts, report generation) SHALL
run inside a dedicated `ci.yml` architecture-coverage job that is independently schedulable from
repository lint, coverage/Sonar, and the other pull-request validation jobs. Because this job does
not share a runner or checkout with any job that builds the CLI/Testing projects, it SHALL build
those projects itself before invoking the CLI in `--no-build` mode.

#### Scenario: Coverage steps run after acceptance in the same job

- **WHEN** the architecture-coverage job runs
- **THEN** it builds `ArchLinterNet.Cli` and `ArchLinterNet.Testing` itself before running the
  strict/audit JSON generation steps
- **AND** it does not depend on a build performed by the repository-lint job or any other job

### Requirement: PR comment posting is a stage of the single validate job

Posting the architecture coverage PR comment SHALL run as a step inside the architecture-coverage
job, alongside the strict/audit analysis and artifact uploads it reports on, rather than in a
separate job or workflow.

#### Scenario: A single job runs the whole pipeline

- **WHEN** `ci.yml` is inspected
- **THEN** the architecture-coverage job includes the strict/audit architecture coverage analysis,
  artifact upload steps, and the PR comment step
- **AND** that job's `permissions` include `pull-requests: write` so the comment step can run
  without a second job

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

### Requirement: SonarCloud quality gate is enforced for trusted runs
Trusted repository pushes and pull requests SHALL fail the CI workflow when the SonarCloud quality gate fails or when required SonarCloud configuration is missing.

#### Scenario: Quality gate failure fails the workflow
- **WHEN** SonarCloud finishes analysis for a trusted push or trusted pull request
- **THEN** the workflow waits for the SonarCloud quality gate result
- **AND** the job fails if the quality gate fails

#### Scenario: Missing configuration fails closed with an actionable diagnostic
- **WHEN** a trusted push or trusted pull request does not have the required SonarCloud configuration
- **THEN** the workflow fails
- **AND** it prints an explicit diagnostic naming the missing secret or variable

#### Scenario: Fork pull request cannot access repository secrets
- **WHEN** a pull request comes from a fork where GitHub does not expose repository secrets
- **THEN** the workflow explains that SonarCloud analysis is skipped for that run
- **AND** it does not falsely report a successful SonarCloud quality gate for that fork analysis

### Requirement: Pull requests expose reviewer-visible SonarCloud results
Trusted pull requests SHALL expose SonarCloud results to reviewers through GitHub PR decoration and a direct path to the SonarCloud pull-request analysis.

#### Scenario: Pull request shows SonarCloud quality-gate feedback
- **WHEN** SonarCloud analyzes a trusted pull request
- **THEN** GitHub shows the Sonar-created pull-request status/check for that analysis
- **AND** reviewers can navigate directly to the SonarCloud pull-request analysis

#### Scenario: Pull request gate is evaluated on new code
- **WHEN** SonarCloud evaluates a trusted pull request
- **THEN** the quality gate is applied to the new code introduced by that pull request
- **AND** the repository does not require the entire historical codebase to be clean before the pull request can merge

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

### Requirement: Core unit suite runs as a deterministic, duration-based shard matrix

The `unit_tests` job SHALL partition `ArchLinterNet.Core.Tests` into a fixed number of deterministic shards, each independently schedulable as its own matrix leg crossed with the existing platform axis, instead of running the entire Core unit assembly as one bucket per platform.

Shard membership SHALL be defined by explicit `FullyQualifiedName` filter tokens committed in `make/test.mk`, with exactly one shard defined as the negated remainder of every other shard's tokens so that a newly added Core unit test is always covered by exactly one shard without requiring a manual assignment step.

Random, timing-dependent, or history-dependent test selection SHALL NOT be used to form shards. Every authoritative pull request SHALL still execute the complete Core unit suite across the shards combined.

#### Scenario: A newly added Core unit test is always covered

- **WHEN** a new test method is added to an existing or new fixture class in
  `ArchLinterNet.Core.Tests` without any change to the shard filter tokens in `make/test.mk`
- **THEN** the test matches the remainder shard's filter by construction
- **AND** it runs in exactly one shard when the full `unit_tests` matrix executes

#### Scenario: Shard legs run independently per platform

- **WHEN** the `unit_tests` job matrix is inspected
- **THEN** each supported platform runs every Core unit shard as its own matrix leg
- **AND** no shard leg declares a `needs:` edge on another shard leg

#### Scenario: The aggregate local unit command still runs everything

- **WHEN** a developer runs `make test-unit` locally
- **THEN** every Core unit shard, and every test outside `ArchLinterNet.Core.Tests` that the unit
  bucket already covered before sharding, executes
- **AND** the command's overall pass/fail result reflects all shards combined

### Requirement: Mechanical shard-membership validation is fail-closed

The repository SHALL provide an automated check, run as part of `make lint`, that discovers every test in `ArchLinterNet.Core.Tests` and verifies it against the shard filter tokens defined in `make/test.mk`.

The check SHALL fail when a shard filter token matches zero discovered tests, and SHALL fail when a shard filter token also matches a test already assigned to the E2E or packed-artifact bucket.

#### Scenario: A dead shard token fails the check

- **WHEN** a shard filter token in `make/test.mk` no longer matches any discovered
  `ArchLinterNet.Core.Tests` test (for example, after a fixture class is renamed or removed without
  updating the token)
- **THEN** the shard-membership check fails with a diagnostic naming the dead token

#### Scenario: A shard token colliding with an E2E or packed-artifact fixture fails the check

- **WHEN** a shard filter token's `FullyQualifiedName` substring match also matches a test already
  assigned to the E2E bucket or the packed-artifact bucket
- **THEN** the shard-membership check fails with a diagnostic naming the colliding token and the
  bucket it leaked into

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

### Requirement: Packed-artifact PR validation fans out one immutable candidate across isolated scenario shards

Pull-request CI SHALL prepare one ephemeral, non-publishable, manifest-bound Checkpoint B candidate and SHALL execute the required packed-artifact scenario inventory as deterministic scenario shards on isolated Windows and Apple Silicon macOS runners. Shards SHALL consume the same candidate manifest/package digest set for the workflow run and SHALL NOT share mutable NuGet caches, tool-install directories, fixture outputs, or temporary state.

The existing authoritative check contexts `Packed Artifact Test Suite (Windows)` and `Packed Artifact Test Suite (Apple Silicon macOS)` SHALL remain stable fan-in checks. A fan-in check SHALL fail when candidate preparation fails, any producer shard fails, shard evidence is missing, or the shard scenario union cannot be merged into a complete canonical platform record.

#### Scenario: Independent Checkpoint B work runs concurrently

- **WHEN** pull-request packed-artifact validation starts after the immutable candidate is prepared
- **THEN** package/entrypoint, two adopter-runtime, five consumer-cleanup, and three public-API-selector shards run as independently schedulable jobs per supported PR platform
- **AND** no shard depends on another shard
- **AND** every shard consumes the same candidate manifest digest
- **AND** each platform fan-in depends only on that platform's shard matrix

#### Scenario: Branch-protection check names remain stable

- **WHEN** the sharded producer jobs finish
- **THEN** CI emits `Packed Artifact Test Suite (Windows)` and `Packed Artifact Test Suite (Apple Silicon macOS)` as the authoritative fan-in contexts
- **AND** those contexts succeed only after complete platform shard evidence is merged and validated

#### Scenario: PR candidate packaging is not release publication

- **WHEN** PR CI prepares the packed-artifact candidate
- **THEN** it uses an ephemeral prerelease version scoped to that workflow run
- **AND** the artifact is used only as test input and is never published, tagged, or treated as an official release candidate

### Requirement: Release workflow separates repository correctness from immutable packed-candidate proof

The release workflow SHALL execute repository lint/unit/ordinary-E2E correctness and strict OpenSpec validation once, build version-bound candidate binaries after any repository gate that recompiles ordinary development-version outputs, bind those passed results to the immutable candidate manifest, and SHALL validate the packed candidate separately through the Checkpoint B platform/shard matrix. Generic repository-acceptance stages SHALL NOT rerun the packed-artifact scenario matrix before or after the authoritative immutable-candidate Checkpoint B execution.

Local `make acceptance` SHALL remain the complete lint + unit + ordinary E2E + packed-artifact convenience gate.

#### Scenario: Release preparation does not run a disposable packed candidate

- **WHEN** `prepare-candidate` validates repository correctness before publication authorization
- **THEN** it runs the repository acceptance surface without packed-artifact proof
- **AND** it later creates one immutable candidate that the Checkpoint B shards consume

#### Scenario: Repository acceptance recompiles ordinary development-version outputs

- **WHEN** repository acceptance has rebuilt source projects before release packaging
- **THEN** `prepare-candidate` rebuilds the release-version binaries before its `--no-build` package steps
- **AND** the installed CLI reports the manifest-bound candidate version

#### Scenario: Evidence aggregation does not rerun acceptance

- **WHEN** all canonical platform evidence is ready
- **THEN** the final release-evidence job consumes repository-gate evidence already bound to the candidate manifest
- **AND** it does not invoke `make acceptance` or another command that reruns Checkpoint B

### Requirement: Dedicated architecture-policy workflow
GitHub Actions SHALL run a dedicated architecture-policy workflow for pull
requests and pushes to `main`. Its strict self-policy job SHALL run the
authoritative read-only `make lint-architecture` target and SHALL not publish
packages, releases, GitHub Pages content, or repository commits.

#### Scenario: Main push refreshes strict-policy status
- **WHEN** code is pushed to `main`
- **THEN** the dedicated workflow restores the required dependencies and runs
  `make lint-architecture`
- **AND** its workflow conclusion is the public architecture-policy badge
  source

#### Scenario: Pull request validates strict policy without publication
- **WHEN** the dedicated workflow runs for a pull request
- **THEN** it executes the same strict self-policy target
- **AND** it uses only read permission for repository contents
- **AND** it does not publish packages, releases, Pages content, or repository
  commits

### Requirement: Scheduled and manually dispatched Git-parser fuzzing
GitHub Actions SHALL provide a dedicated fuzzing workflow for the synthetic Git
binary-parser harness. The workflow SHALL run only on a schedule or explicit
manual dispatch, use fixed SharpFuzz and AFL++ image versions, and execute the
campaign with no container network access, a 100 ms per-case timeout, and a
512 MiB memory limit.

The workflow SHALL not run from ordinary pull-request CI. It SHALL report the
candidate crash/hang count in the workflow summary, SHALL encrypt candidate
inputs with the repository `GIT_PARSER_FUZZ_TRIAGE_KEY` secret, SHALL retain
only the encrypted bundle and integrity sidecar for 14 days, and SHALL remove
raw findings from the ephemeral runner after upload.

#### Scenario: Ordinary pull request validation
- **WHEN** a pull request changes code or the fuzzing harness
- **THEN** ordinary CI runs deterministic repository checks and regressions but
  does not start the coverage-guided campaign

#### Scenario: Scheduled campaign
- **WHEN** the fuzz workflow is triggered on its schedule or manually
- **THEN** it materializes the committed synthetic corpus, verifies the pinned
  toolchain, and executes the bounded no-network AFL++ campaign
