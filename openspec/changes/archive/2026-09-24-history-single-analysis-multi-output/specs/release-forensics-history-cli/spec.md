## ADDED Requirements

### Requirement: Single-analysis multi-output reporting
`analyze` SHALL accept a repeatable `--report <format>=<destination>` option, where
`<format>` is `json` or `markdown` and `<destination>` is `stdout`, `stderr`, or a
file path. When one or more `--report` values are supplied, the CLI SHALL run
`Ingest` exactly once and render only the formats requested by at least one
sink from that single finalized `HistoryIngestionResult`, using the same
canonical JSON and Markdown writers as the `--format` path. `--format` SHALL
be ignored when `--report` is supplied.

The CLI SHALL reject, before performing any write, two `--report` sinks that
resolve to the same destination (including two sinks both targeting `stdout`,
both targeting `stderr`, or two file sinks whose paths resolve to the same
location) and a file destination that resolves to the same path as `--policy`.
A rejected `--report` configuration SHALL leave standard output empty and
exit non-zero without calling `Ingest`.

File destinations SHALL be written using an atomic temporary-file-then-rename
sequence. Every file sink SHALL be staged and validated before any stream
sink (`stdout` or `stderr`) is written, and `stdout` SHALL be written before
`stderr` when both are configured. Staged file renames SHALL be committed
only after every sink's content has been produced without error. When any
sink fails — a destination collision, a file write error, or the JSON
Unicode/serialization failure already defined for the report-serialization
diagnostic — the run SHALL fail closed: no sink SHALL receive a report, a
diagnostic naming the failed destination(s) SHALL be written to standard
error, and the process SHALL exit non-zero. The CLI SHALL NOT claim to
replace multiple independent file destinations atomically as a set, but SHALL
NOT leave a destination that a consumer could mistake for a complete
successful multi-format result either.

JSON written to `stdout` through `--report` SHALL use the same raw UTF-8
without-BOM boundary as the existing `--format json` stdout path. JSON
written to `stderr` or a file destination SHALL use ordinary text writes.

#### Scenario: One ingestion serves two formats
- **WHEN** `history analyze --from <a> --to <b> --report json=report.json --report markdown=report.md` succeeds
- **THEN** `Ingest` runs exactly once and both `report.json` and `report.md`
  are written from the same finalized result, matching the content each
  would have if run individually with `--format json` and `--format markdown`
  respectively

#### Scenario: Existing single-format invocation is unaffected
- **WHEN** `history analyze --from <a> --to <b> --format markdown` runs without `--report`
- **THEN** behavior and output bytes are identical to the pre-existing
  `--format markdown` contract

#### Scenario: Duplicate destination rejected before ingestion
- **WHEN** `--report json=out.json --report markdown=out.json` is authored (same file path for two formats)
- **THEN** the command fails with a duplicate-destination diagnostic, `Ingest`
  is never called, and standard output stays empty

#### Scenario: Report destination collides with the policy input
- **WHEN** `--policy policy.yml --report json=policy.yml` is authored
- **THEN** the command fails with a destination-collision diagnostic before
  any write occurs

#### Scenario: Partial failure leaves no destination looking complete
- **WHEN** one of two configured file sinks cannot be written (for example, an
  unwritable directory)
- **THEN** neither file sink is committed, no stream sink is written, a
  diagnostic identifies the failed destination, and the process exits
  non-zero
