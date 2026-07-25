## 1. CLI options and parsing

- [x] 1.1 Add `ReportSink` record type (format + destination) to the CLI layer
- [x] 1.2 Add `IReadOnlyList<ReportSink> AdditionalSinks` to `ValidateCommandOptions`
- [x] 1.3 Add `--report <format>=<destination>` repeatable option to `ValidateCommandDefinition`
- [x] 1.4 Parse `--report` values: split on `=`, validate format (`human`/`json`/`sarif`), resolve destination (`stdout`/`stderr`/file path)
- [x] 1.5 Reject invalid format, empty destination, and duplicate destinations with exit code 2
- [x] 1.6 Update help text to document `--report`, stdout/stderr/file routing, and multi-sink behavior
- [x] 1.7 Add `--report` parsing tests to `ValidateCommandDefinitionTests`

## 2. ReportCoordinator

- [x] 2.1 Create `ReportCoordinator` class in the CLI layer that receives outcomes and sink list
- [x] 2.2 Implement single-outcome routing: build format string once per unique format, dispatch to each configured destination
- [x] 2.3 Implement combined-outcome routing: reuse merged JSON/SARIF documents from `WriteCombinedOutcome`, route to sinks
- [x] 2.4 Wire ReportCoordinator into `ValidateCommandHandler`, replacing inline format-then-write logic
- [x] 2.5 Preserve backward compat: `--format human` with no `--report` produces identical output

## 3. Atomic file output

- [x] 3.1 Implement atomic file writer: write to `<path>.tmp`, then `File.Move(tmp, path, overwrite: true)`, delete tmp on failure
- [x] 3.2 Pre-validate all file destinations: check writability, no input-file collision (policy/baseline/snapshot paths), no duplicate destinations
- [x] 3.3 Implement two-phase write: all content generated and temp-written before any rename begins
- [x] 3.4 Handle write failures: report `output-failed` (no output) or `partial-output` (some succeeded), return exit code 2

## 4. Error reporting and typed status

- [x] 4.1 Extend error output shape to include `output_status: "output-failed" | "partial-output"` for file-sink failures
- [x] 4.2 Ensure validation exit code (0 or 1) is preserved when all sinks succeed
- [x] 4.3 Update `WriteExecutionError` in handler to handle output failures distinctly from validation failures

## 5. Documentation

- [x] 5.1 Update `cli-validation` spec delta to reflect `--format` as stdout selector + `--report` addition
- [x] 5.2 Add user-facing docs for `--report` flag with examples (Bash, PowerShell, CI-neutral)
- [x] 5.3 Archive OpenSpec change and update main specs

## 6. Tests

- [x] 6.1 Add `--report` parsing tests (valid formats, invalid formats, empty destination, duplicate destinations)
- [x] 6.2 Add `ReportCoordinator` unit tests (single sink, multi-sink, combined-mode multi-sink)
- [x] 6.3 Add atomic file-write tests (success, failure, input-file collision)
- [x] 6.4 Add integration-style tests verifying exit code 2 for output failures
- [x] 6.5 Verify all sink tests pass without re-analysis (mock `ICliRuntime` to assert format methods called exactly once per unique format)
