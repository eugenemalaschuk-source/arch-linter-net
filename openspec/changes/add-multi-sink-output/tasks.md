## 1. CLI options and parsing

- [ ] 1.1 Add `ReportSink` record type (format + destination) to the CLI layer
- [ ] 1.2 Add `IReadOnlyList<ReportSink> AdditionalSinks` to `ValidateCommandOptions`
- [ ] 1.3 Add `--report <format>=<destination>` repeatable option to `ValidateCommandDefinition`
- [ ] 1.4 Parse `--report` values: split on `=`, validate format (`human`/`json`/`sarif`), resolve destination (`stdout`/`stderr`/file path)
- [ ] 1.5 Reject invalid format, empty destination, and duplicate destinations with exit code 2
- [ ] 1.6 Update help text to document `--report`, stdout/stderr/file routing, and multi-sink behavior
- [ ] 1.7 Add `--report` parsing tests to `ValidateCommandDefinitionTests`

## 2. ReportCoordinator

- [ ] 2.1 Create `ReportCoordinator` class in the CLI layer that receives outcomes and sink list
- [ ] 2.2 Implement single-outcome routing: build format string once per unique format, dispatch to each configured destination
- [ ] 2.3 Implement combined-outcome routing: reuse merged JSON/SARIF documents from `WriteCombinedOutcome`, route to sinks
- [ ] 2.4 Wire ReportCoordinator into `ValidateCommandHandler`, replacing inline format-then-write logic
- [ ] 2.5 Preserve backward compat: `--format human` with no `--report` produces identical output

## 3. Atomic file output

- [ ] 3.1 Implement atomic file writer: write to `<path>.tmp`, then `File.Move(tmp, path, overwrite: true)`, delete tmp on failure
- [ ] 3.2 Pre-validate all file destinations: check writability, no input-file collision (policy/baseline/snapshot paths), no duplicate destinations
- [ ] 3.3 Implement two-phase write: all content generated and temp-written before any rename begins
- [ ] 3.4 Handle write failures: report `output-failed` (no output) or `partial-output` (some succeeded), return exit code 2

## 4. Error reporting and typed status

- [ ] 4.1 Extend error output shape to include `output_status: "output-failed" | "partial-output"` for file-sink failures
- [ ] 4.2 Ensure validation exit code (0 or 1) is preserved when all sinks succeed
- [ ] 4.3 Update `WriteExecutionError` in handler to handle output failures distinctly from validation failures

## 5. Documentation

- [ ] 5.1 Update `cli-validation` spec delta to reflect `--format` as stdout selector + `--report` addition
- [ ] 5.2 Add user-facing docs for `--report` flag with examples (Bash, PowerShell, CI-neutral)
- [ ] 5.3 Archive OpenSpec change and update main specs

## 6. Tests

- [ ] 6.1 Add `--report` parsing tests (valid formats, invalid formats, empty destination, duplicate destinations)
- [ ] 6.2 Add `ReportCoordinator` unit tests (single sink, multi-sink, combined-mode multi-sink)
- [ ] 6.3 Add atomic file-write tests (success, failure, input-file collision)
- [ ] 6.4 Add integration-style tests verifying exit code 2 for output failures
- [ ] 6.5 Verify all sink tests pass without re-analysis (mock `ICliRuntime` to assert format methods called exactly once per unique format)
