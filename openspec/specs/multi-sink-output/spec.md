# multi-sink-output Specification

## Purpose
TBD - created by archiving change add-multi-sink-output. Update Purpose after archive.
## Requirements
### Requirement: CLI accepts --report for additional output sinks

The CLI SHALL accept a repeatable `--report` option with format `<format>=<destination>`. The format SHALL be one of `human`, `json`, or `sarif`. The destination SHALL be `stdout`, `stderr`, or a file path. The option MAY be specified multiple times to configure multiple sinks.

#### Scenario: Single --report with file destination
- **WHEN** the CLI is invoked with `--format human --report json=results.json`
- **THEN** human-readable output SHALL appear on stdout AND a JSON document SHALL be written to `results.json`

#### Scenario: Multiple --report flags
- **WHEN** the CLI is invoked with `--report json=ci.json --report sarif=ci.sarif`
- **THEN** JSON SHALL be written to `ci.json` and SARIF SHALL be written to `ci.sarif`, in addition to the default human output on stdout

#### Scenario: --report human=stderr
- **WHEN** the CLI is invoked with `--format json --report human=stderr`
- **THEN** JSON SHALL appear on stdout and human-readable output SHALL appear on stderr

#### Scenario: Invalid format in --report
- **WHEN** the CLI is invoked with `--report xml=out.xml`
- **THEN** exit code 2 SHALL be returned with an error message

#### Scenario: Invalid destination in --report
- **WHEN** the CLI is invoked with `--report json=`
- **THEN** exit code 2 SHALL be returned with an error message

### Requirement: --format selects the stdout format

The `--format` option SHALL determine which format is written to stdout. `--format human|json|sarif` SHALL remain the default (human) and SHALL behave identically to before this change when no `--report` flags are present.

#### Scenario: --format human with no --report
- **WHEN** the CLI is invoked with `--format human` and no `--report` flags
- **THEN** behavior and output SHALL be identical to before this change

#### Scenario: --format json with no --report
- **WHEN** the CLI is invoked with `--format json` and no `--report` flags
- **THEN** behavior and output SHALL be identical to before this change

### Requirement: One analysis serves all sinks

All configured sinks SHALL consume the same normalized validation result. No additional policy composition, project discovery, assembly loading, or contract execution SHALL occur because of additional sinks.

#### Scenario: --report does not increase analysis counts
- **WHEN** the CLI is invoked with `--report json=out.json --report sarif=out.sarif`
- **THEN** policy composition, project discovery, assembly loading, and contract execution counts SHALL be identical to a single-format invocation

### Requirement: Output files are written atomically

File sinks SHALL be written to a temporary file in the same directory as the target path, then atomically renamed to the target path on success. On failure, the temporary file SHALL be deleted. All file writes for a given invocation SHALL complete before any rename occurs, to avoid partial output.

#### Scenario: Successful file write
- **WHEN** a `--report json=results.json` sink writes successfully
- **THEN** `results.json` SHALL contain the complete JSON document

#### Scenario: Write failure produces no partial file
- **WHEN** a `--report json=results.json` file write fails
- **THEN** `results.json` SHALL NOT exist (pre-existing file SHALL be preserved)

### Requirement: Output failures are reported distinctly

If a file sink fails, the CLI SHALL still report the validation outcome. The exit code SHALL be 2 when one or more required output sinks failed. The error output SHALL include a typed status distinguishing `output-failed` (no sinks wrote) from `partial-output` (some sinks wrote, some failed).

#### Scenario: Output failure returns exit code 2
- **WHEN** a `--report json=/readonly/results.json` write fails
- **THEN** exit code SHALL be 2 and the error SHALL contain `output_status: "output-failed"`

### Requirement: Input files are protected from overwrite

The CLI SHALL reject a `--report` destination that matches the path of any input file (policy, baseline, snapshot, or other loaded contract file). The CLI SHALL also reject duplicate destinations across `--report` flags.

#### Scenario: Overwriting policy file is rejected
- **WHEN** the CLI is invoked with `--report json=architecture/dependencies.arch.yml`
- **THEN** exit code 2 SHALL be returned with an error message

#### Scenario: Duplicate destinations are rejected
- **WHEN** the CLI is invoked with `--report json=out.json --report sarif=out.json`
- **THEN** exit code 2 SHALL be returned with an error message

### Requirement: Multi-mode combined output routes to all sinks

When `--mode strict,audit` is used with `--report` flags, the merged JSON and SARIF documents SHALL be written to each configured sink.

#### Scenario: Combined JSON written to file
- **WHEN** the CLI is invoked with `--mode strict,audit --report json=combined.json`
- **THEN** `combined.json` SHALL contain a single JSON document with one result per mode

### Requirement: Stdout and stderr behavior is documented

The help text SHALL describe which output goes to stdout, stderr, and files for every combination of `--format` and `--report`.

#### Scenario: Help text documents output routing
- **WHEN** a user invokes `--help`
- **THEN** the help text SHALL describe stdout, stderr, and file output behavior

