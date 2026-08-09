## Why

Consumers that request `--format json` cannot reliably parse failure output because several CLI command handlers emit a human-text error on an owned configuration or build-state termination path. This makes automated use require command-specific fallback parsing precisely when a command has failed.

## What Changes

- Make JSON the authoritative stdout document for the owned baseline and public-API terminating paths that previously emitted human text despite `--format json`.
- Define a stable, typed JSON error envelope for those paths that identifies the failure category and preserves actionable policy or build-state diagnostics when available.
- Preserve current exit-code semantics and the readable, backward-compatible human stderr output.
- Add integration coverage that parses configuration and build-state JSON failures, including every command family exposing JSON output.

## Capabilities

### New Capabilities

<!-- None. -->

### Modified Capabilities

- `cli-validation`: validation JSON output remains parseable for owned configuration and preflight failures.
- `baseline-generation`: baseline subcommands emit one parseable JSON failure document when JSON is selected.
- `policy-check-command`: policy check preserves its structured JSON contract for command failures.
- `graph-export-command`: graph JSON requests retain their structured error document on owned failures.
- `explain-command`: explain JSON requests retain their structured error document on owned failures.
- `public-api-snapshots`: public API JSON commands preserve a single parseable document for configuration and build-state failures.

## Impact

Affected code is limited to existing CLI command handlers and shared error-formatting helpers, with CLI integration/unit tests and output-format documentation updated to describe the JSON error envelope. No Core validation semantics, exit-code changes, or new command surface is introduced.
