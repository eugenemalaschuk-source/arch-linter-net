## Why

The fail-closed CLI validation added for unknown input can still be bypassed when
`--help` or `--version` appears before an invalid token. That permits malformed
invocations to return success, contradicting the CLI exit-code contract.

## What Changes

- Validate the complete argument vector before retaining legacy `--help` and
  `--version` rendering behavior.
- Return exit code 2 with a diagnostic when help or version is combined with an
  unknown command, subcommand, or option.
- Add regressions for help/version followed by invalid input while preserving
  successful standalone help and version invocations.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `cli-validation`: Help and version only return success when the complete
  command line is valid.

## Impact

- Affects `CliHost` command-line dispatch and CLI integration/host coverage.
- No public API, dependency, or command-surface additions.
