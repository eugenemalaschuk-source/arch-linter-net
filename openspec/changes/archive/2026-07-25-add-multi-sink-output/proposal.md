## Why

Users commonly need readable local output and a machine-readable CI artifact from one validation run. Currently this requires two invocations (e.g. `--format human` for the terminal then `--format json` for CI), which wastes work, doubles expensive failure-path execution, and can produce inconsistent evidence. Issue #353 (0.5.0 First Ice adoption) identified this as a P1 blocker.

## What Changes

- Add repeatable `--report <format>=<destination>` CLI option to route output beyond stdout
- `--format human|json|sarif` stays as the stdout sink selector (default: human) — backward compatible
- `--report human=stderr` sends human text to stderr
- `--report json=report.json` writes JSON file alongside stdout output
- `--report sarif=report.sarif` writes SARIF file
- Introduce a `ReportCoordinator` that normalizes diagnostics once, formats for each requested sink, and routes to destination (stream or file) without re-analysis
- File writes use atomic temp+replace pattern; pre-validate all destinations before writing
- Output failures return exit code 2 with typed status distinguishing `output-failed` vs `partial-output`
- `--output` stays reserved for baseline/API-snapshot/graph artifacts — does not participate in report routing
- Testing API (`ArchLinterNet.Testing`) unchanged — direct typed consumer of Core API
- Validate command multi-mode path (`--mode strict,audit`) builds merged JSON/SARIF documents once, routes to all configured sinks

## Capabilities

### New Capabilities
- `multi-sink-output`: One CLI invocation emits human, JSON, and/or SARIF output to configurable destinations (stdout, stderr, files) from a single normalized validation result — no re-analysis per format

### Modified Capabilities
- `cli-validation`: `--format` semantics refined from "the output format" to "the stdout output format selector"; `--report` option added

## Impact

- `src/ArchLinterNet.Cli/Commands/Validate/ValidateCommandOptions.cs` — new fields for report destinations
- `src/ArchLinterNet.Cli/Commands/Validate/ValidateCommandDefinition.cs` — new `--report` option, updated help text
- `src/ArchLinterNet.Cli/Commands/Validate/ValidateCommandHandler.cs` — new output routing via ReportCoordinator; extract format-string building from direct ICliConsole writes
- `src/ArchLinterNet.Cli/Infrastructure/CliRuntime.cs` — new `FormatResult*` methods already exist as string-returning; no breaking changes to ICliRuntime
- No changes to `ArchLinterNet.Core` formatters — they already return strings
- New file: report coordinator / sink dispatch logic
- Tests: handler tests for `--report` parsing, file-writing, atomicity, error reporting
