## MODIFIED Requirements

### Requirement: Setup proves capabilities fail-closed

Setup SHALL treat provider-plan declarations as cost metadata only. Required
checks, Rules API access, OIDC availability/claims, provider quota, account
identity, and deployment capability SHALL come from the bounded live inspector
for v1. The live provider inspector SHALL use only fields and successful
capability responses documented by the provider contract; it SHALL not require
an undocumented account plan catalogue field to establish account capability.
An unsigned or caller-authored local capability-evidence JSON file SHALL never
satisfy a prerequisite; `--capability-evidence` SHALL fail closed with a stable
diagnostic until an authenticated evidence protocol is shipped. Missing or
contradictory live evidence SHALL prevent non-dry-run writes.

#### Scenario: Unsigned capability evidence is not proof

- **WHEN** a user supplies a fresh JSON file that claims `required_check`,
  `rules_api`, `oidc`, `relay`, and quota are true
- **THEN** setup and doctor do not treat those claims as capabilities
- **AND** the command reports that authenticated live inspection is required

#### Scenario: Declared plan is insufficient proof

- **WHEN** a user supplies `--provider-plan pro` without capability evidence
- **THEN** Relay setup remains unavailable
- **AND** it does not mark required check, Rules API, OIDC, or quota prerequisites
  as satisfied

#### Scenario: Documented account response proves account identity without a plan slug

- **WHEN** the provider Account Details response contains the documented account
  `id`, name, and type fields but no `plan.slug`
- **THEN** the inspector accepts the account identity and evaluates Worker and
  Durable Object capability responses separately
- **AND** a supported configured provider plan remains cost metadata rather than
  an undocumented API assertion

### Requirement: Generated renewal schedule matches the bounded cadence

For every valid renewal cadence in minutes, the generated workflow SHALL
declare UTC cron trigger slots anchored at midnight. Adjacent slots, including
the last slot of one UTC day and the first slot of the next UTC day, SHALL be
separated by at least the configured cadence. The declared slots SHALL equal
the preview's `floor(1440 / cadence_minutes)` jobs per day; multiple POSIX cron
entries MAY be used when one expression cannot represent a non-hour-aligned
interval. The workflow SHALL not substitute a broader hourly or otherwise more
frequent schedule.

#### Scenario: Ninety-minute renewal preserves the previewed bound

- **WHEN** setup generates an enabled renewal workflow for a 90-minute cadence
- **THEN** its schedules represent `00:00, 01:30, 03:00, ... , 22:30` UTC
- **AND** the workflow declares 16 trigger slots per UTC day
- **AND** it does not declare the hourly `0 * * * *` schedule

#### Scenario: Hour-aligned renewal remains compact and exact

- **WHEN** setup generates an enabled renewal workflow for a 120-minute
  cadence
- **THEN** its schedules represent every even UTC hour
- **AND** the workflow declares 12 trigger slots per UTC day
- **AND** it declares no additional minute or hour slots

#### Scenario: Non-divisible cadence remains safe across midnight

- **WHEN** setup generates an enabled renewal workflow for a cadence such as
  31 or 59 minutes
- **THEN** the final slot of a UTC day and the first slot of the next UTC day
  are separated by at least the configured cadence
- **AND** the generated slot count equals the preview's floor-based daily count

## ADDED Requirements

### Requirement: Provider plan metadata is optional

The Relay setup configuration MAY set `provider_plan` to `null`. When present,
the value SHALL be limited to the shipped closed metadata set, but its presence
or value SHALL not be required to satisfy any capability or deployment
prerequisite.

#### Scenario: Proven capabilities do not require a plan label

- **WHEN** live account, quota, required-check, Rules API, and OIDC inspection
  all succeed and `provider_plan` is `null`
- **THEN** Relay setup remains eligible for a valid plan
- **AND** no provider capability is inferred from a plan label

### Requirement: Generated producer context and artifact transport are executable

The generated producer workflow SHALL derive pull-request number, base ref,
and base commit from supported GitHub pull-request event context. It SHALL
upload the generated badge and semantic evidence from paths accepted by the
artifact action, without using a workspace-only file-matching predicate to
guard a runner-temporary path. Missing required files SHALL fail the producer
or upload step rather than silently skipping evidence publication.

#### Scenario: Producer binds a real pull-request context

- **WHEN** the generated producer runs for a pull request
- **THEN** its manifest contains the event's pull-request number, base ref, base
  SHA, head SHA, and head tree SHA
- **AND** pull-request number, run ID, and run attempt are JSON integers
- **AND** it does not read nonexistent `GITHUB_EVENT_NUMBER` or
  `GITHUB_BASE_SHA` environment variables

#### Scenario: Producer uploads generated temporary artifacts

- **WHEN** the producer creates its badge and health artifacts under the runner
  temporary directory
- **THEN** both upload steps execute when the files exist
- **AND** an absent required artifact produces an explicit failure instead of a
  skipped upload

### Requirement: Github-raw doctor probes the publisher's artifact branch

For `github-raw` configurations, doctor SHALL construct its artifact URL from
the fixed publication branch used by the generated publisher and README, not
from the producer's pull-request base ref. The probe SHALL preserve the
configured repository identity and artifact path.

#### Scenario: Doctor checks the published raw branch

- **WHEN** a generated github-raw configuration targets a non-default producer
  base ref
- **THEN** doctor probes
  `/<owner>/<repository>/architecture-health-badge/architecture-health.json`
- **AND** it does not probe the producer base branch as the artifact branch
