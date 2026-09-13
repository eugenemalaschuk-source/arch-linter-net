## ADDED Requirements

### Requirement: Optional renewal output follows the enabled state

The generated Relay consumer bundle SHALL include a scheduled renewal workflow
only when renewal is enabled in the approved configuration. When renewal is
disabled, the registry event allowlist and managed-file list SHALL not claim or
contain a scheduled renewal workflow.

#### Scenario: Disabled renewal emits no scheduled workflow

- **WHEN** setup generates a valid Relay bundle with `renewal.enabled` set to
  `false`
- **THEN** the bundle contains no scheduled renewal workflow
- **AND** the registry allowlist contains `push` but not `schedule`
- **AND** the managed-file list contains no renewal workflow path

#### Scenario: Enabled renewal emits the scheduled workflow

- **WHEN** setup generates a valid Relay bundle with `renewal.enabled` set to
  `true`
- **THEN** the bundle contains exactly one scheduled renewal workflow
- **AND** the registry allowlist contains both `push` and `schedule`
- **AND** the managed-file list contains the renewal workflow path

### Requirement: Generated renewal schedule matches the bounded cadence

For every valid renewal cadence in minutes, the generated workflow SHALL
declare UTC cron trigger slots anchored at midnight and separated by that
cadence until the end of the UTC day. The declared slots SHALL equal the
preview's `ceil(1440 / cadence_minutes)` jobs per day; multiple POSIX cron
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
