## Why

The CLI already supports human and JSON output, but JSON is a custom shape suited to CI artifacts, not to standard static-analysis viewers. SARIF is the industry-standard diagnostics format consumed by GitHub code scanning and similar tools. Without SARIF, ArchLinterNet violations cannot be surfaced in those pipelines (issue #65).

## What Changes

- Add a `sarif` value to the CLI's `--format`/`-f` flag on the `validate` command, alongside the existing `human` and `json` values.
- Add a new `ArchitectureSarifFormatter` that renders SARIF 2.1.0 output for the same violations and cycles already produced by validation — no new detection logic.
- Map each diagnostic's `ContractId` (falling back to the normalized contract name when absent) to a stable, deduplicated SARIF `rule.id`.
- Derive SARIF `level` (`error`/`warning`) from the CLI's `--mode` flag (`strict`/`audit`) for the whole run.
- Include `physicalLocation` (file + line) for method-body diagnostics, which are the only diagnostic kind that currently carries parseable file/line information.
- Include `logicalLocations` (fully-qualified name + a lightweight kind hint) for all other diagnostic kinds, which identify a type/namespace/assembly/package rather than a file position.
- Preserve deterministic ordering of SARIF results, matching the existing violation sort order.
- Update `PrintHelp()` to document the new format value.
- Existing `human` and `json` output SHALL remain byte-identical.

## Capabilities

### New Capabilities
- `sarif-diagnostics-output`: SARIF 2.1.0 rendering of architecture violations and cycles — envelope shape, rule mapping, severity mapping, and location mapping (physical vs logical).

### Modified Capabilities
- `cli-validation`: the `--format`/`-f` flag gains a `sarif` value alongside `human`/`json`.

## Impact

- `src/ArchLinterNet.Cli/Program.cs`: extend format validation, add a SARIF dispatch branch, update help text.
- `src/ArchLinterNet.Core/Reporting/`: new `ArchitectureSarifFormatter` class (kept separate from the existing `ArchitectureDiagnosticFormatter.cs`, which is already near this repo's file-size lint threshold).
- Tests: `tests/ArchLinterNet.Core.Tests/` (new formatter unit tests) and `tests/ArchLinterNet.Cli.Tests/CliIntegrationTests.cs` (new `--format sarif` end-to-end test).
- No changes to detection/checker logic, YAML policy schema, or existing human/JSON output.
