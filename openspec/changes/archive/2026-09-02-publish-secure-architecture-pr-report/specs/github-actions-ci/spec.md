## ADDED Requirements

### Requirement: Architecture PR report producer runs in the existing CI workflow

The architecture report producer SHALL run strict/audit coverage, canonical Health/change
artifacts, and the CLI-rendered PR report artifact inside a dedicated read-only `ci.yml`
job that is independently schedulable from repository lint, coverage/Sonar, and the other
pull-request validation jobs. Because this job does not share a runner or checkout with a job that
builds the CLI/Testing projects, it SHALL build those projects itself before invoking the CLI in
`--no-build` mode. It SHALL not have pull-request write permission or a comment-writing step.

#### Scenario: Producer builds and renders independently

- **WHEN** the architecture report producer job runs
- **THEN** it builds `ArchLinterNet.Cli` and `ArchLinterNet.Testing` before it runs the coverage,
  Health, change, and report CLI steps
- **AND** it does not depend on a build performed by repository lint or another job

## MODIFIED Requirements

### Requirement: Pull request validation runs as independently schedulable jobs

`ci.yml` SHALL decompose pull request validation into independently schedulable jobs along real
dependency boundaries instead of one monolithic job, so unrelated checks can start concurrently
and fail independently. At minimum the workflow SHALL provide: a workflow-quality job (actionlint,
zizmor, workflow Prettier check), a repository-lint job (`make lint`), independently schedulable
.NET coverage shard jobs, a coverage/Sonar aggregation job (SonarCloud begin/build/end lifecycle,
Python tooling coverage, coverage artifact import, Codecov upload), an architecture PR-report
producer job (strict/audit coverage analysis and immutable report artifact upload), and a
tooling/support-tests job (the workflow-contract and release-evidence Python test suites).
`needs:` SHALL be used only along genuine artifact/result boundaries; specifically the
coverage/Sonar aggregation job MAY depend on the .NET coverage shard jobs whose reports it
consumes, while unrelated validation jobs remain schedulable at PR start.

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

#### Scenario: Architecture report producer builds what it needs independently

- **WHEN** the architecture report producer job runs on its own runner and checkout
- **THEN** it builds the projects required by strict/audit coverage and canonical report generation
  itself, rather than relying on a build performed by a different job
- **AND** it preserves fail-closed strict-mode semantics while artifact upload remains available
  for completed canonical report data

#### Scenario: No artificial ordering between unrelated validation jobs

- **WHEN** workflow-quality, repository-lint, .NET coverage shards, coverage/Sonar aggregation,
  architecture report producer, and tooling/support-tests jobs are inspected
- **THEN** only the coverage/Sonar aggregation job depends on .NET coverage shards because it
  genuinely consumes their artifacts
- **AND** unrelated jobs declare no artificial `needs:` edges between each other

## REMOVED Requirements

### Requirement: Architecture coverage analysis runs in the existing CI workflow

**Reason**: The former requirement couples coverage analysis to a legacy coverage-comment path
and does not describe generation of the canonical unified report artifact.

**Migration**: Use the read-only architecture PR report producer requirement and retain coverage
artifacts within that producer.

### Requirement: PR comment posting is a stage of the single validate job

**Reason**: A pull-request code execution job cannot safely hold the credential that writes a
review comment.

**Migration**: Publish the CLI-rendered artifact through the dedicated completed-CI publisher
defined by `architecture-pr-report-publication`.
