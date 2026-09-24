## Why

`history analyze` runs canonical Git ingestion and co-change scoring before it picks a
`--format`. A consumer who needs both the JSON artifact and the Markdown view for the same
`--repository`/`--from`/`--to`/`--policy` range currently has no way to get both from one
completed analysis: two full CLI invocations are required, and each repeats ingestion and
scoring for the same evidence. Issue #1016 blocks enabling release-forensics in the regular
release publication path until this duplicate work is eliminated; fixing it now is a rework
fix to an already-shipped command, not new Git-analysis semantics.

## What Changes

- Add a repeatable `--report <format>=<destination>` option to `history analyze`, reusing the
  `format=destination` syntax already shipped on the root `validate` command's `--report`
  option (`stdout`, `stderr`, or a file path; format is `json` or `markdown`).
- When one or more `--report` sinks are supplied, `history analyze` calls
  `HistoryPolicyIngestionService.Ingest(...)` exactly once, renders only the formats actually
  requested through the existing `HistoryIngestionJsonWriter`/`HistoryIngestionTextWriter`, and
  writes each rendered document to its sink. No ingestion, scoring, normalization, or canonical
  JSON logic is duplicated or cloned.
- Existing `--format json|markdown` single-destination stdout behavior is unchanged when
  `--report` is not supplied — byte-identical to today, preserving the current two-invocation
  workflow for any caller that has not adopted `--report`.
- Reject, before any write, duplicate `--report` destinations (two sinks resolving to the same
  file path, or two sinks both targeting `stdout`/`stderr`) and a file destination that collides
  with the `--policy` input path.
- File sinks are written with the CLI's existing atomic temp-file-then-rename pattern
  (`IFileSystem.WriteAllTextToTemp` / `RenameTempToTarget`). All file sinks are staged and
  validated before any stream sink (`stdout`/`stderr`) is written; renames are committed only
  after every sink has produced valid content. A collision, write error, or JSON
  Unicode/serialization failure fails the whole run closed: no sink receives a report, a
  diagnostic is written to standard error, and the process exits non-zero. Independent files are
  not claimed to be replaced atomically as a set — but the CLI never leaves output that a
  consumer could mistake for a complete, successful multi-format result.
- JSON written to stdout keeps going through the existing `ICliConsole.WriteCanonicalJson`
  raw-UTF8-without-BOM boundary; JSON written to `stderr` or a file uses ordinary text writes,
  matching the existing `--report` precedent on the root command.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `release-forensics-history-cli`: `history analyze` gains a repeatable `--report
  <format>=<destination>` option and a single-analysis multi-output completion contract
  (collision rejection, atomic per-file staging, fail-closed behavior on partial failure), while
  the existing `--format`/stdout default behavior is preserved unchanged.

## Impact

- `src/ArchLinterNet.Cli/Commands/History/Application/HistoryCommandDefinition.cs` — new
  `--report` option, parsed alongside the existing options.
- `src/ArchLinterNet.Cli/Commands/History/Application/HistoryIngestCommandOptions.cs` — carries
  the parsed report sinks (or a parse error) through to the handler.
- `src/ArchLinterNet.Cli/Commands/History/Application/HistoryIngestCommandHandler.cs` — single
  ingestion call, sink-aware rendering/writing, fail-closed staging/commit sequence.
- New `HistoryReportSink`/sink-parsing type(s) under
  `src/ArchLinterNet.Cli/Commands/History/Application/`, mirroring
  `Commands/Validate/Application/ReportSink.cs`'s syntax without introducing a shared/competing
  reporting framework.
- `src/ArchLinterNet.Cli/Commands/History/EntryPoint/HistoryCommandModule.cs` — passes
  `IFileSystem` into the handler for atomic file writes.
- `docs/usage/output-formats.md`, `docs/guides/history-forensics.md` and CLI help text — document
  the new `--report` option.
- `tests/ArchLinterNet.Cli.Tests/HistoryIngestCommandHandlerTests.cs` — regression coverage for
  single-ingestion multi-output, collision rejection, and fail-closed partial-failure behavior.
- `openspec/specs/release-forensics-history-cli/spec.md` — delta describing the new requirement.
