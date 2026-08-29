## Why

Unknown top-level tokens and subcommands can currently fall through to successful parent help, allowing a misspelled command to report exit code `0` in CI. This contradicts the documented invalid-argument contract and risks false-green automation.

## What Changes

- Reject unrecognised top-level CLI input and subcommands with exit code `2`, a diagnostic that names the token, and a `--help` usage hint.
- Preserve successful exit code `0` behavior for explicit valid root and subcommand help requests.
- Pin invalid-option and unknown-command behavior with focused black-box CLI regression tests.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `cli-validation`: Invalid top-level input becomes an explicitly specified invalid-argument case that fails closed with exit code `2`.

## Impact

The change affects the central CLI host, CLI process-invocation regression tests, and the documented CLI validation contract. It introduces no command, public API, dependency, or policy-schema changes.
