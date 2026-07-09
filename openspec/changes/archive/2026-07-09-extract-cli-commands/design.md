## Context

`src/ArchLinterNet.Cli/Program.cs` was a `static partial class Program` combining three things: the `Main` entry-point dispatch, the full `validate` command implementation (option parsing, engine invocation, all three output formats, help text), and the `graph`/`explain` command implementations. A second file, `Program.Baseline.cs`, held the `baseline` subcommand family (`generate`/`update`/`prune`/`diff`/`verify`), and a third, `Program.BaselineHelp.cs`, held that family's help text. Together these three files were ~1,350 lines, and `Program.cs` alone mixed unrelated concerns (bootstrap, one command's full logic, two more commands' full logic) with no separation between "how the CLI is wired" and "what each command does."

Issue #202's non-goals rule out redesigning the CLI framework, changing command names/flags/outputs/exit codes, or reworking the application-layer services (`ArchitectureEngine`, `ArchitectureEngineBuilder`) behind the CLI — this is a pure code-organization change.

## Goals / Non-Goals

**Goals:**
- Each CLI command (`validate`, `graph`, `explain`, `baseline` + its five subcommands) has its own class, independently readable and extensible without touching `Program.cs`.
- `Program.cs` contains only `Main`'s dispatch logic.
- Preserve every argument, output string, and exit code exactly — this is process-boundary-visible behavior with 112 existing black-box integration tests asserting on it.
- Preserve the self-policy seam rule (`ArchLinterNet.Cli` may only reference `Core.Model`, `Core.Reporting`, `Core.Validation`, plus `Core.Composition`/`Core.Graph` for engine construction and graph-level types) and namespace-coverage without needing YAML edits.

**Non-Goals:**
- Redesigning the CLI framework or argument-parsing approach (still hand-rolled `switch` loops, matching every other command).
- Changing command names, flags, outputs, or exit-code semantics.
- Reworking `ArchitectureEngine`/`ArchitectureEngineBuilder` or any other `Core` service.
- Combining this with unrelated CLI feature work.
- Introducing a DI container or command-framework abstraction (e.g. `System.CommandLine`) for the CLI — no concrete problem requires it, and the issue explicitly scopes this to extraction, not framework replacement.

## Decisions

### 1. Namespace: `ArchLinterNet.Cli.Commands`, not a new project or assembly
The issue suggests "a dedicated CLI commands namespace... or an equivalent reviewed namespace consistent with the repository style." Keeping it as a sub-namespace of `ArchLinterNet.Cli` (rather than a new project) avoids any assembly-boundary/self-policy-layer changes: `architecture/dependencies.arch.yml`'s `namespace-coverage` contract roots at `ArchLinterNet.Cli` and matches by namespace prefix, and the seam contract (`cli-must-use-validation-application-seam`) is keyed off the `cli` layer, which is declared as `namespace: ArchLinterNet.Cli` — a sub-namespace inherits both without any YAML edit. Confirmed by running `make lint-architecture` after the change: the `self-architecture-policy` strict test still passes.

### 2. One class per top-level command; `BaselineCommand` covers all five subcommands
`ValidateCommand`, `GraphCommand`, and `ExplainCommand` map one-to-one with their original `RunXxxCommand` methods. `BaselineCommand` is a single class (partial, split into `BaselineCommand.cs` and `BaselineCommand.Help.cs` mirroring the original two-file split) covering `generate`/`update`/`prune`/`diff`/`verify`, rather than five separate classes — they share dispatch (`Run` inspects `args[0]`), share the `FormatBaselineComparisonForHumans`/`FormatBaselineComparisonAsJson`/`FormatEntryForJson`/`AppendEntryLines` helpers, and the issue's acceptance criteria only requires the family be represented as "a focused command unit," not one class per subcommand.

### 3. `CliEngine` as the shared-state seam, not per-command instantiation or constructor injection
`Program`'s original `_formatter`, `_sarifFormatter`, and `_engine` fields (plus the free-standing `TryParseLevel` method, called identically from both `graph` and `explain`) were private statics shared across every command via the partial class. Since each command becomes an independent class, that sharing needs an explicit home. A new internal static class, `CliEngine`, holds these — preserving the original `Lazy<ArchitectureEngine>` single-construction-per-process semantics and avoiding a DI container or constructor-injection pattern that nothing else in this small, single-purpose CLI uses today.

### 4. No OpenSpec-visible behavior change; new capability documents the *structure*, not new behavior
Every existing CLI-behavior spec (`cli-validation`, `explain-command`, `baseline-generation`, `graph-export-command`, `self-architecture-policy`) remains true unmodified — this change doesn't touch what the CLI does, only where its code lives. Per the `extract-family-validation-from-loader` precedent (which added `policy-document-validation-pipeline` for the same reason), a new capability (`cli-command-dispatch`) is added to describe the organizational requirement itself, so a future contributor adding a command back into `Program.cs` directly is caught as a spec regression rather than silently accepted.

## Risks / Trade-offs

- **[Risk] A behavior-preserving "mechanical" extraction still introduces a subtle argument-parsing or output regression** → Mitigation: every command method body is a verbatim lift (no logic rewrites); the existing 112 `CliIntegrationTests` (black-box, process-invocation) are the regression gate and were run unchanged, all passing.
- **[Risk] Missing a `using` directive after the move causes a namespace resolution surprise (e.g. `ArchitectureGraphLevel` lives in `Core.Model`, not `Core.Graph`)** → Caught immediately by `dotnet build`; fixed during implementation before running tests.
- **[Risk] This retrospective archive was filed after implementation, so the "propose before apply" ordering benefit (catching design issues before code is written) doesn't apply here** → Accepted: the reviewer-flagged gap is about governance/documentation completeness, not about redoing the implementation; the design decisions above reflect what was actually built and verified.

## Migration Plan

Not applicable in the deployment sense — `ArchLinterNet.Cli` is a .NET tool (`arch-linter-net`), and this change is entirely internal to its `Exe` project with no public API surface. No data migration, no version bump beyond the normal release process. Rollback is a plain revert if `make acceptance` regresses.

## Open Questions

None outstanding — the namespace-coverage/seam question (would this need a `dependencies.arch.yml` edit?) was the only open unknown and is resolved by Decision 1, confirmed by a passing `make lint-architecture` run.
