## Why

`ArchLinterNet.Cli`'s `Program.cs` (`src/ArchLinterNet.Cli/Program.cs`, plus `Program.Baseline.cs` and `Program.BaselineHelp.cs`) kept every CLI command's full implementation — `validate`, `graph`, `explain`, and the `baseline` subcommand family (`generate`/`update`/`prune`/`diff`/`verify`) — as `static partial class Program` methods, ~1,350 lines combined. Every new command or subcommand meant growing this one class further, the same god-file pattern already addressed elsewhere in the codebase for `ArchitectureContractRunner` (#138) and `ArchitecturePolicyDocumentLoader` (#210). Issue #202 (parent story #183) asks to extract command implementations into focused classes/namespaces, keeping `Program.cs` limited to host/bootstrap/dispatch.

**Note:** this proposal is filed retrospectively. The extraction (see #225) was implemented and merged before this OpenSpec record was created; a review pass on #202 flagged that the propose→apply→archive lifecycle followed by comparable refactors (`extract-runner-setup-services`, `shrink-runner-into-session`, `extract-family-validation-from-loader`) had been skipped. This archive entry documents what was actually built so the pattern is governed going forward, matching those precedents.

## What Changes

- Introduce `ArchLinterNet.Cli.Commands`, a new namespace under `src/ArchLinterNet.Cli/Commands/`, holding one class per command: `ValidateCommand`, `GraphCommand`, `ExplainCommand`, `BaselineCommand` (+ `BaselineCommand.Help.cs` partial for its subcommand help text, mirroring the original `Program.cs`/`Program.BaselineHelp.cs` split).
- Introduce `CliEngine`, an internal static class holding the shared `Lazy<ArchitectureEngine>`, `ArchitectureDiagnosticFormatter`, `ArchitectureSarifFormatter`, and the `TryParseLevel` helper that were previously private fields/methods on `Program` and duplicated across `graph`/`explain` dispatch.
- Each command class's public `Run(string[] args)` is a verbatim lift of the corresponding `RunXxxCommand` method body — same option parsing, same validation, same output, same exit codes.
- `Program.cs` shrinks to `Main(string[] args)` dispatch only: it inspects `args[0]` and delegates to the matching command class's `Run`.
- `Program.Baseline.cs` and `Program.BaselineHelp.cs` are deleted; their contents move into `Commands/BaselineCommand.cs` and `Commands/BaselineCommand.Help.cs`.

## Capabilities

### New Capabilities
- `cli-command-dispatch`: defines the requirement that `ArchLinterNet.Cli`'s `Program.cs` performs only argument-based dispatch, that each CLI command's implementation lives in its own class under `ArchLinterNet.Cli.Commands`, and that this reorganization preserves existing CLI behavior, output, and exit codes exactly.

### Modified Capabilities
(none — `cli-validation`, `explain-command`, `baseline-generation`, `graph-export-command`, and `self-architecture-policy` describe CLI *behavior*, which is unchanged by this internal reorganization.)

## Impact

- `src/ArchLinterNet.Cli/Program.cs` — shrinks from ~540 lines to ~20 (dispatch only).
- `src/ArchLinterNet.Cli/Program.Baseline.cs`, `Program.BaselineHelp.cs` — removed.
- New: `src/ArchLinterNet.Cli/Commands/CliEngine.cs`, `ValidateCommand.cs`, `GraphCommand.cs`, `ExplainCommand.cs`, `BaselineCommand.cs`, `BaselineCommand.Help.cs`.
- `architecture/dependencies.arch.yml` — unchanged. The `namespace-coverage` strict-coverage contract roots at `ArchLinterNet.Cli` and matches by namespace prefix, so `ArchLinterNet.Cli.Commands` is automatically covered under the existing `cli` layer; verified via `make lint-architecture` (the `self-architecture-policy` strict test) passing unchanged.
- `ArchLinterNet.Cli.Tests` (`CliIntegrationTests.*`, 112 tests) — unchanged. These are black-box, process-invocation integration tests (they build and run the CLI executable), so they exercise the refactor as a regression gate without needing edits themselves.
- No changes to `ArchLinterNet.Core` or its public surface.
