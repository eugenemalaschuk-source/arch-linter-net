## 1. Shared infrastructure

- [x] 1.1 Create `src/ArchLinterNet.Cli/Commands/` folder.
- [x] 1.2 Add `CliEngine` internal static class holding the shared `Lazy<ArchitectureEngine>`, `ArchitectureDiagnosticFormatter`, `ArchitectureSarifFormatter`, and the `TryParseLevel` helper (moved verbatim from `Program.cs`).

## 2. Extract command classes (verbatim method-body lifts, same options/output/exit codes)

- [x] 2.1 `ValidateCommand` (from `RunValidateCommand` + `PrintHelp`).
- [x] 2.2 `GraphCommand` (from `RunGraphCommand` + `PrintGraphHelp`).
- [x] 2.3 `ExplainCommand` (from `RunExplainCommand` + `PrintExplainHelp`).
- [x] 2.4 `BaselineCommand` (from `Program.Baseline.cs`: dispatch + `generate`/`update`/`prune`/`diff`/`verify` + comparison-formatting helpers).
- [x] 2.5 `BaselineCommand.Help.cs` (from `Program.BaselineHelp.cs`: all baseline subcommand help text).

## 3. Rewire the entry point

- [x] 3.1 Rewrite `Program.cs` to only dispatch `args[0]` (`baseline`/`graph`/`explain`/default → `validate`) to the matching command class's `Run`.
- [x] 3.2 Delete `Program.Baseline.cs` and `Program.BaselineHelp.cs`.
- [x] 3.3 Fix `using` directives surfaced by the move (`ArchitectureGraphLevel` resolves from `ArchLinterNet.Core.Model`, not `Core.Graph`; `ArchitectureEngine`/`ArchitectureEngineBuilder` from `Core.Composition`).

## 4. Self-policy verification

- [x] 4.1 Run `make lint-architecture` (the `self-architecture-policy` strict test) and confirm it passes unchanged — no `architecture/dependencies.arch.yml` edit needed, since `namespace-coverage`'s `ArchLinterNet.Cli` root and the `cli-must-use-validation-application-seam` contract both match `ArchLinterNet.Cli.Commands` by namespace prefix.

## 5. Tests

- [x] 5.1 Build the full solution (`dotnet build`) and confirm zero errors/warnings.
- [x] 5.2 Run `ArchLinterNet.Cli.Tests` (`CliIntegrationTests.*`, 112 black-box process-invocation tests) unchanged — all pass, proving behavior parity.

## 6. Validation gate

- [x] 6.1 Run `make fmt` (no C# formatting changes needed).
- [x] 6.2 Run `make acceptance`; confirm green (938 Core tests, 112 CLI tests, 3 Unity tests, 0 failures).

## 7. OpenSpec lifecycle (retrospective)

- [x] 7.1 File this change record (`proposal.md`, `design.md`, `tasks.md`, `specs/cli-command-dispatch/spec.md`) after the fact, per reviewer feedback on #202 that the propose→apply→archive lifecycle followed by `extract-runner-setup-services`, `shrink-runner-into-session`, and `extract-family-validation-from-loader` was skipped for this refactor.
- [x] 7.2 Merge `cli-command-dispatch`'s spec delta into `openspec/specs/cli-command-dispatch/spec.md`.
