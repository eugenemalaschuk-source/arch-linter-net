# multi-sink-output Specification

## Purpose

Routes a single validation result to multiple output sinks (stdout, stderr, files) without re-analysis per format.

## Requirements

### Requirement: CLI accepts --report for additional output sinks

The CLI SHALL accept a repeatable `--report` option with format `<format>=<destination>`. The format SHALL be one of `human`, `json`, or `sarif`. The destination SHALL be `stdout`, `stderr`, or a file path. The option MAY be specified multiple times to configure multiple sinks. `--format`/`--json` are separate one-sink legacy forms and SHALL NOT be combined with `--report`.

#### Scenario: Single --report with file destination
- **WHEN** the CLI is invoked with `--report json=results.json`
- **THEN** a JSON document SHALL be written to `results.json` and no implicit output SHALL appear on stdout

#### Scenario: Multiple --report flags
- **WHEN** the CLI is invoked with `--report json=ci.json --report sarif=ci.sarif`
- **THEN** JSON SHALL be written to `ci.json` and SARIF SHALL be written to `ci.sarif`

#### Scenario: --report to stderr and file
- **WHEN** the CLI is invoked with `--report json=stdout --report human=stderr`
- **THEN** JSON SHALL appear on stdout and human-readable output SHALL appear on stderr

#### Scenario: Invalid format in --report
- **WHEN** the CLI is invoked with `--report xml=out.xml`
- **THEN** exit code 2 SHALL be returned with an error message

#### Scenario: Invalid destination in --report
- **WHEN** the CLI is invoked with `--report json=`
- **THEN** exit code 2 SHALL be returned with an error message

### Requirement: --format is a legacy one-sink form

The `--format` option SHALL be a separate one-sink legacy form. When `--format human|json|sarif` is used without `--report` flags, behavior SHALL be identical to before this change. Combining `--format`/`--json` with `--report` SHALL be rejected as ambiguous.

#### Scenario: --format human with no --report (legacy)
- **WHEN** the CLI is invoked with `--format human` and no `--report` flags
- **THEN** behavior and output SHALL be identical to before this change

#### Scenario: --format json with no --report (legacy)
- **WHEN** the CLI is invoked with `--format json` and no `--report` flags
- **THEN** behavior and output SHALL be identical to before this change

#### Scenario: --format combined with --report is rejected
- **WHEN** the CLI is invoked with `--format json --report human=stderr`
- **THEN** exit code 2 SHALL be returned with an error that format and report cannot be combined

### Requirement: One analysis serves all sinks

All configured sinks SHALL consume the same normalized validation result. No additional policy composition, project discovery, assembly loading, or contract execution SHALL occur because of additional sinks.

#### Scenario: --report does not increase analysis counts
- **WHEN** the CLI is invoked with `--report json=out.json --report sarif=out.sarif`
- **THEN** policy composition, project discovery, assembly loading, and contract execution counts SHALL be identical to a single-format invocation

### Requirement: Output files are written atomically

File sinks SHALL be written to a uniquely-named temporary file in the same directory as the target path, then atomically renamed to the target path on success. On failure, the temporary file SHALL be deleted. All file writes for a given invocation SHALL complete before any rename occurs, to avoid partial output.

#### Scenario: Successful file write
- **WHEN** a `--report json=results.json` sink writes successfully
- **THEN** `results.json` SHALL contain the complete JSON document

#### Scenario: Write failure produces no partial file
- **WHEN** a `--report json=results.json` file write fails
- **THEN** `results.json` SHALL NOT exist (pre-existing file SHALL be preserved)

### Requirement: Output failures are reported distinctly

If a file sink fails, the CLI SHALL still report the validation outcome. The exit code SHALL be 2 when one or more required output sinks failed. The typed error evidence SHALL distinguish committed, uncommitted (staged), and failed destinations. `output-failed` indicates no file sinks committed; `partial-output` indicates at least one file sink committed while others failed.

#### Scenario: Output failure returns exit code 2
- **WHEN** a `--report json=/readonly/results.json` write fails
- **THEN** exit code SHALL be 2 and the error SHALL contain `output_status: "output-failed"`

### Requirement: Input files are protected from overwrite

The CLI SHALL reject a `--report` destination that matches the path of any input file (policy, baseline, imported policy files, or other loaded contract files). The CLI SHALL also reject duplicate destinations across `--report` flags, treating case-insensitive paths as equivalent.

#### Scenario: Overwriting policy file is rejected
- **WHEN** the CLI is invoked with `--report json=architecture/dependencies.arch.yml`
- **THEN** exit code 2 SHALL be returned with an error message

#### Scenario: Duplicate destinations are rejected
- **WHEN** the CLI is invoked with `--report json=out.json --report sarif=out.json`
- **THEN** exit code 2 SHALL be returned with an error message

### Requirement: Multi-mode combined output routes to all sinks

When `--mode strict,audit` is used with `--report` flags, the merged JSON and SARIF documents SHALL be written to each configured sink. When `--mode strict,audit` is used with `--format human` (legacy, no `--report`), output SHALL be byte-compatible with pre-#364 behavior.

#### Scenario: Combined JSON written to file via --report
- **WHEN** the CLI is invoked with `--mode strict,audit --report json=combined.json`
- **THEN** `combined.json` SHALL contain a single JSON document with one result per mode

### Requirement: Stdout and stderr behavior is documented

The help text SHALL describe which output goes to stdout, stderr, and files for every combination of `--format` and `--report`.

#### Scenario: Help text documents output routing
- **WHEN** a user invokes `--help`
- **THEN** the help text SHALL describe stdout, stderr, and file output behavior
