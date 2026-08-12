## ADDED Requirements

### Requirement: Pull request validation runs as independently schedulable jobs

`ci.yml` SHALL decompose pull request validation into independently schedulable jobs along real
dependency boundaries instead of one monolithic job, so unrelated checks can start concurrently
and fail independently. At minimum the workflow SHALL provide: a workflow-quality job (actionlint,
zizmor, workflow Prettier check), a repository-lint job (`make lint`), a coverage/Sonar job (the
SonarCloud begin/build/end lifecycle, coverage-only unit tests, Python tooling coverage, Codecov
upload), an architecture-coverage job (strict/audit architecture coverage analysis, artifact
uploads, PR comment), and a tooling/support-tests job (the architecture-coverage-report-generator
and release-evidence-aggregator Python test suites). `needs:` SHALL be used between these jobs
only where one genuinely requires another's artifact or result; by default all of them are
schedulable at PR start alongside the `unit_tests`/`e2e_tests`/`packed_artifact_tests` jobs.

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
- **THEN** it runs only the coverage-only unit test target, Python tooling coverage, SonarCloud
  begin/end for trusted pull requests, and the Codecov upload
- **AND** it does not invoke the E2E or packed-artifact test buckets

#### Scenario: Architecture coverage job builds what it needs independently

- **WHEN** the architecture-coverage job runs on its own runner and checkout
- **THEN** it builds the projects required by the strict/audit architecture coverage analysis
  itself, rather than relying on a build performed by a different job
- **AND** it preserves fail-closed strict-mode failure semantics even though artifact upload and
  PR comment steps use `always()`/`continue-on-error` for report publication

#### Scenario: No artificial ordering between the new validation jobs

- **WHEN** the set of workflow-quality, repository-lint, coverage/Sonar, architecture-coverage, and
  tooling/support-tests jobs is inspected
- **THEN** none of them declares a `needs:` edge on another unless that job genuinely consumes an
  artifact or result the other produces

## MODIFIED Requirements

### Requirement: SonarCloud analysis runs in the existing CI workflow

SonarCloud analysis SHALL run inside `ci.yml`'s coverage/Sonar job so the repository reuses the
same checkout metadata, restore, build, and test execution that powers the coverage-only unit test
run, without introducing a second standalone restore/build/test pipeline only for SonarCloud.

#### Scenario: SonarCloud reuses the existing validation path

- **WHEN** the coverage/Sonar job runs on a trusted push or pull request
- **THEN** it performs SonarCloud analysis inside the same job as the coverage-only unit test run
- **AND** it does not introduce a second standalone restore/build/test pipeline only for SonarCloud

#### Scenario: Pull request analysis has enough Git metadata

- **WHEN** SonarCloud analyzes a pull request
- **THEN** the coverage/Sonar job checks out the repository with full history
- **AND** SonarCloud receives enough branch comparison metadata to evaluate the pull request
  against its base branch

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
