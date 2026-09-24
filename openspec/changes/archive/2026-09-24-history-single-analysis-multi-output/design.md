## Context

`HistoryIngestCommandHandler.Execute` currently calls `HistoryPolicyIngestionService.Ingest(...)`
unconditionally, then branches on `--format` to write exactly one document to stdout. A consumer
who wants both JSON and Markdown for the same range runs the CLI twice, which repeats Git object
reads, TaskKey extraction, and co-change scoring in full for the second call. The root `validate`
command already solved an analogous problem with a repeatable `--report <format>=<destination>`
option and a staging/commit `ReportCoordinator` (`src/ArchLinterNet.Cli/Commands/Validate/Application/ReportSink.cs`,
`ReportCoordinator.cs`); that coordinator is intentionally general (three formats, multiple
validation modes, cancellation, timing) and is `internal` to the `Validate.Application` namespace.

## Goals / Non-Goals

**Goals:**
- One `Ingest` call serves every requested output format in a single process invocation.
- Syntax parity with the existing `--report format=destination` convention so CLI users learn one
  pattern.
- Fail-closed behavior at least as strong as the existing single-format path: a collision, write
  error, or Unicode/serialization failure must not leave any destination holding a
  report that looks complete.
- Zero behavior change to the existing `--format`-only invocation (still the default, still used
  by every caller that hasn't adopted `--report`).

**Non-Goals:**
- Building a shared/generalized reporting framework consumed by both `validate` and `history`.
  The proposal explicitly asks to reuse *syntax*, not to extract `ReportCoordinator` into a
  shared abstraction — `history` only ever has two formats and no validation-mode fan-out, so the
  generalized coordinator's complexity (multi-mode combination, timing hooks, cancellation
  tokens) is not justified here.
- Atomic replacement of the file set as a whole. Independent files cannot be swapped atomically
  across a filesystem boundary; the contract is explicit fail-closed signaling, not cross-file
  atomicity.
- Any Git traversal/ingestion performance optimization beyond removing the duplicate `Ingest`
  call (explicitly out of scope per the issue).
- Cancellation-token plumbing: `HistoryPolicyIngestionService.Ingest` has no cancellation support
  today and this change does not add it.

## Decisions

**Decision: New small `HistoryReportSink`/parser type local to `Commands/History/Application`,
not a shared type with `Validate`.**
`ReportSink`/`ReportDestinationType` in `Commands/Validate/Application/ReportSink.cs` are
`internal sealed` to that namespace. Making them cross-command shared types would be an
architecture change (a new shared abstraction) unjustified by two call sites with different
format sets and no other coupling. Instead, `history` gets its own minimal
`HistoryReportSink(string Format, HistoryReportDestinationType DestinationType, string? FilePath)`
and a small parser function that mirrors `ValidateCommandDefinition.ParseReportSinks`'s syntax
and dedup-by-destination rule, restricted to `json`/`markdown`. This keeps `Core.Scanning`-style
boundary discipline (`Cli` command families stay decoupled from each other) while giving users
one consistent flag shape.

**Decision: Parse `--report` in `HistoryCommandDefinition`, carry a nullable parse-error string
through `HistoryIngestCommandOptions`, same as `ValidateCommandDefinition`/`ValidateCommandOptions`.**
This is the established pattern for a `System.CommandLine` option that needs custom validation
beyond what the option's own parser can express, and keeps `HistoryIngestCommandHandler` free of
`System.CommandLine` concerns.

**Decision: Stage-then-commit sequencing mirrors `ReportCoordinator.DistributeToSinks`, scaled
down.**
1. Render only the content formats actually requested by at least one sink (never render a
   format nothing asked for).
2. For JSON content, run the existing strict-UTF-8 validity check
   (`HistoryReportOutputWriter`'s encoder) before anything is written anywhere; on failure, emit
   `report_serialization_invalid` to stderr and stop — matches the existing single-format
   contract exactly.
3. Stage every file sink via `IFileSystem.WriteAllTextToTemp`, re-read and re-validate the temp
   file (size bound, and JSON re-parse for `json` sinks) — reusing the same validation shape as
   `ReportCoordinator.StageFileSink`/`ValidateWrittenTempFile` and `ReportCommandHandler`'s
   existing temp-then-rename usage.
4. Only if every file sink staged cleanly, write stream sinks (`stdout` before `stderr`, matching
   `ReportCoordinator`'s ordering rationale: a failed stdout must not leave a misleading
   successful stderr).
5. Commit staged renames last. Any staging or stream-write failure deletes already-staged temp
   files and writes one diagnostic naming every failed destination; the process exits non-zero
   and no destination receives a report.

**Decision: JSON stdout keeps the raw-byte `ICliConsole.WriteCanonicalJson` boundary; JSON to
file/stderr uses ordinary text writes.**
This matches the existing precedent set by the root `validate` command's own `--report` sinks
(file/stderr JSON go through ordinary `TextWriter`/`IFileSystem` writes there too) and keeps the
`UTF-8 canonical JSON stdout boundary` requirement scoped to stdout, as the current spec already
states.

**Decision: Reject a `--report` file destination equal to the `--policy` input path, mirroring
`report pr`'s `FindOutputCollision`.**
`history analyze` has exactly one file-shaped input option (`--policy`); checking collision
against it before writing is cheap and consistent with the existing `report pr` precedent.
`--repository` is a directory being read, not a single file that a report write could silently
clobber, so no equivalent check is needed there.

## Risks / Trade-offs

- **[Risk]** A partially-staged run (some files staged, none committed yet) could leave stray
  `.tmp` files if the process is killed mid-staging. → Mitigation: same residual risk already
  accepted by `ReportCoordinator`/`ReportCommandHandler` for the existing `--report`/`--output`
  paths; out of scope to solve differently here.
- **[Risk]** Two nearly-identical sink-parsing implementations (`Validate`'s and `History`'s)
  could drift in error-message wording over time. → Mitigation: accepted trade-off per the
  Non-Goals decision above; the two commands have different format sets and this keeps
  `Cli` command boundaries independent, which the design decisions above value over DRY across
  unrelated command families.
- **[Trade-off]** `--report` sinks silently ignore `--format` when both are supplied. → Mitigation:
  matches the existing `validate` precedent exactly, so behavior is already familiar; documented
  in CLI help and `docs/usage/output-formats.md`.

## Migration Plan

Purely additive CLI surface; no migration needed. Existing `--format`-only invocations are
byte-identical before and after. No rollback plan beyond reverting the change, since no persisted
state or schema changes.

## Open Questions

None outstanding — all resolved via the decisions above.
