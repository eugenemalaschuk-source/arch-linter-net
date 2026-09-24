## 1. Sink type and parsing

- [x] 1.1 Add `HistoryReportDestinationType` enum and `HistoryReportSink` record under
      `src/ArchLinterNet.Cli/Commands/History/Application/`.
- [x] 1.2 Add a `--report <format>=<destination>` sink parser (json/markdown formats;
      stdout/stderr/file destinations; duplicate-destination rejection) mirroring
      `ValidateCommandDefinition.ParseReportSinks`'s syntax and error messages.

## 2. CLI option wiring

- [x] 2.1 Add the repeatable `--report` option to `HistoryCommandDefinition`, parse it into
      sinks with a captured parse-error string, and extend `HistoryIngestCommandOptions` with
      `ReportSinks`/`ReportParseError` (default empty/null so existing positional test
      constructors keep compiling).
- [x] 2.2 Update the `analyze` help text (`HistoryIngestCommandHandler.Usage` and/or a fuller
      help block) to document `--report`.
- [x] 2.3 Pass `IFileSystem` into `HistoryIngestCommandHandler` via `HistoryCommandModule`.

## 3. Handler: single-ingestion multi-output

- [x] 3.1 Surface the report parse error (if any) before calling `Ingest`.
- [x] 3.2 Validate `--report` file destinations against `--policy` for a collision before
      calling `Ingest`.
- [x] 3.3 When `ReportSinks` is empty, keep the existing `--format`-only code path unchanged.
- [x] 3.4 When `ReportSinks` is non-empty: call `Ingest` once; on a diagnostic outcome, write it
      to stderr and return the existing error exit code (no sinks touched).
- [x] 3.5 Render only the formats requested by at least one sink; validate JSON content's strict
      UTF-8 encodability before any write, matching `HistoryReportOutputWriter`'s existing check.
- [x] 3.6 Stage file sinks via `IFileSystem.WriteAllTextToTemp`, re-validate each staged temp
      file (size bound; re-parse for `json`), and only then write stream sinks (`stdout` before
      `stderr`), and only then commit renames.
- [x] 3.7 On staging/serialization failure, delete already-staged temp files, write one
      diagnostic naming the failed destination(s) to stderr, write nothing to any sink, and exit
      non-zero. On stream-write or later-rename failure, preserve the same non-zero exit while
      reporting the typed partial-output evidence for destinations already delivered/committed.

## 4. Tests

- [x] 4.1 Add a handler test: one `--report json=<path> --report markdown=<path>` run produces
      both files from a single ingestion, with content matching the equivalent single-format
      runs.
- [x] 4.2 Add a handler test asserting existing `--format`-only behavior (JSON and Markdown) is
      unchanged when `--report` is not supplied (reuse/extend existing tests as needed).
- [x] 4.3 Add a handler test for duplicate `--report` destinations failing closed before
      `Ingest` runs.
- [x] 4.4 Add a handler test for a `--report` file destination colliding with `--policy`.
- [x] 4.5 Add a handler test for a `--report` file write failure leaving no sink committed and a
      non-zero exit code.
- [x] 4.6 Add/extend a CLI definition test verifying `--report` is parsed into the option surface
      (mirroring existing `ValidateCommandDefinitionTests` coverage style) if such coverage
      exists for `history`.
- [x] 4.7 Add regression coverage for physical-file collision, Unicode multi-output rendering,
      stream/rename partial output, cancellation propagation, staged-temp cleanup and an
      ingestion counter incremented inside Core's actual `HistoryIngestionService.Ingest`.
- [x] 4.8 Add opt-in timing evidence, a freshly packed/installed CLI acceptance test and an explicit
      packed-package benchmark harness recording the 2-process/1-process call counts, phase
      timings, process overhead, peak working set and exact tool-package SHA-256.

## 5. Docs and spec sync

- [x] 5.1 Update `docs/usage/output-formats.md`'s release forensics section to document
      `--report`.
- [x] 5.2 Update `docs/guides/history-forensics.md` if it shows example invocations that should
      demonstrate the new option.
- [x] 5.3 Compare implementation against `openspec/changes/history-single-analysis-multi-output/specs/release-forensics-history-cli/spec.md`
      and adjust either the code or the delta spec so they match exactly.
- [x] 5.4 Run `openspec validate --all`, then `openspec archive history-single-analysis-multi-output`.
