# CLI Timing Specification

## Purpose
Adds an opt-in CLI flag that reports phase-level timing for a validation run to stderr.

## Requirements

### Requirement: CLI timing flag

The CLI SHALL support a `--timings` boolean flag that enables phase-level timing instrumentation for architecture validation runs.

#### Scenario: --timings flag is accepted
- **WHEN** user passes `--timings` to the CLI
- **THEN** validation runs normally with timing instrumentation enabled

#### Scenario: --timings with --strict
- **WHEN** user passes `--strict --timings`
- **THEN** strict validation runs and timing report is printed to stderr

#### Scenario: --timings with --audit
- **WHEN** user passes `--audit --timings`
- **THEN** audit validation runs and timing report is printed to stderr

#### Scenario: --timings with --json
- **WHEN** user passes `--json --timings`
- **THEN** valid JSON output is written to stdout and timing report is printed to stderr

#### Scenario: --timings with --contract
- **WHEN** user passes `--contract <id> --timings`
- **THEN** only the selected contract(s) are timed, with count=0 for unselected contract families

### Requirement: Timing output to stderr

The timing report SHALL be written to stderr, not stdout, to avoid interfering with normal validation output.

#### Scenario: stdout unaffected
- **WHEN** user runs `arch-linter-net --timings`
- **THEN** stdout contains only the normal validation output (human or JSON)

#### Scenario: stderr contains timing report
- **WHEN** user runs `arch-linter-net --timings`
- **THEN** stderr contains a human-readable timing report

#### Scenario: no --timings means no stderr output change
- **WHEN** user runs `arch-linter-net` without `--timings`
- **THEN** stderr MUST NOT contain any timing report

### Requirement: Deterministic timing report shape

The timing report SHALL have a stable structure (phase names, ordering, indentation) across runs. Only numeric duration values vary by machine.

#### Scenario: phase names are deterministic
- **WHEN** user runs `--timings` against the same policy
- **THEN** the same set of phase names appears in the same order

#### Scenario: contract families with zero contracts are reported
- **WHEN** the policy has no asmdef contracts
- **THEN** the timing report includes "asmdef" with count=0 and 0ms

### Requirement: Phase timing granularity

The timing report SHALL include coarse phases: `load_and_setup`, `configuration_check`, `contract_checks`, and `post_processing`. The `load_and_setup` phase SHALL include sub-phases for `yaml_loading`, `root_resolution`, `condition_set_resolution`, and `assembly_resolution`. The `contract_checks` phase SHALL report per-contract-family durations with contract counts.

#### Scenario: load_and_setup sub-phases reported
- **WHEN** user runs `--timings`
- **THEN** stderr contains indented sub-phases under `load_and_setup`

#### Scenario: per-contract counts reported
- **WHEN** user runs `--timings`
- **THEN** each contract family line includes `count=N`

### Requirement: Exit code preservation

The `--timings` flag SHALL NOT change the exit code compared to running without it under the same conditions.

#### Scenario: passing validation with --timings
- **WHEN** validation passes and `--timings` is enabled
- **THEN** exit code is 0

#### Scenario: failing validation with --timings
- **WHEN** validation fails and `--timings` is enabled
- **THEN** exit code is 1

#### Scenario: runtime error with --timings
- **WHEN** an invalid argument or missing file occurs with `--timings`
- **THEN** exit code is 2 and no timing output is printed
