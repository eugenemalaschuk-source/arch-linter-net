## ADDED Requirements

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

### Requirement: Coverage collection remains a single unsharded run

The coverage-collecting unit test execution (`make test-coverage` and `make test-coverage-main-ci`) SHALL continue to run the complete unsharded `ArchLinterNet.Core.Tests` unit set in a single process, independent of how many shards the non-coverage `unit_tests` CI job uses.

#### Scenario: Coverage is unaffected by the shard count

- **WHEN** `make test-coverage` or `make test-coverage-main-ci` runs
- **THEN** it collects coverage for the complete unit test set in one `dotnet test --collect`
  invocation
- **AND** its behavior does not depend on the `unit_tests` job's shard matrix
