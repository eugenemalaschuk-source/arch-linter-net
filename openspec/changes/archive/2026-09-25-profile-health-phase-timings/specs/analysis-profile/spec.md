## ADDED Requirements

### Requirement: Architecture Health profiling measures one shared evaluation

When the CLI `health` command is run with `--profile`, the system SHALL publish an `analysis-profile/v1` document containing timings for the snapshot preparation and validation work performed by that command, plus aggregate `total` elapsed and processor-time measurements and process resource measurements. Strict and audit evaluation in `--mode all` SHALL be measured through the same immutable analysis snapshot. The `--change-snapshot` composite path SHALL use that same timing and snapshot for Health and change projection.

The profile counters SHALL come from the snapshot used for the Health result. Profile timing SHALL remain opt-in; a Health invocation without `--profile` SHALL not collect timing or write a profile. Collecting a profile SHALL NOT change Health output, findings, or exit category.

#### Scenario: Health profile separates preparation and validation work

- **WHEN** `health --mode all --profile <path>` evaluates a policy
- **THEN** the profile contains the `analysis-profile/v1` schema id, a `total` phase, the actual snapshot-preparation and strict/audit validation phases, populated process measurements, and counters from the snapshot used for the Health result

#### Scenario: Health change snapshot reuses its measured snapshot

- **WHEN** `health --mode all --change-snapshot <snapshot-path> --profile <profile-path>` is run
- **THEN** Health, strict/audit validation, and change-snapshot projection use one measured analysis snapshot, and the profile reports one snapshot materialization

#### Scenario: Health profiling preserves default results

- **WHEN** the same Health request is run with and without `--profile`
- **THEN** the Health JSON or human output, findings, and exit category are identical, and the unprofiled request writes no profile and collects no timing
