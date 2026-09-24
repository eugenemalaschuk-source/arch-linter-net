# release-forensics-history-cli Specification

## Purpose

Define the shipped `history` command family, the canonical Git analysis it runs over an
explicit authored range, the versioned successful report it emits, and the fail-closed
diagnostic surface that replaces a report whenever analysis cannot be canonically completed.
`release-architecture-forensics` remains the semantic authority for what the evidence means;
this capability defines how evidence is produced and surfaced.
## Requirements
### Requirement: History command family and authored range operands
The shipped CLI SHALL expose a `history` command family whose `analyze`
subcommand runs canonical Release Architecture Forensics analysis and emits the
versioned successful report over an explicit authored range.

`analyze` SHALL accept a required `--from` operand, a required `--to` operand,
an optional `--repository` path defaulting to the current directory, an optional
`--policy` path, and an optional `--format` selector accepting `json` (default)
and `markdown`. `--from` is exclusive and `--to` is inclusive.

The repository SHALL be located by walking from the requested path toward the
filesystem root until a Git directory is found, supporting both a `.git`
directory and a `.git` file containing a `gitdir:` pointer. The repository
object-hash format SHALL be read from the repository's own configuration,
defaulting to SHA-1 when no `extensions.objectformat` value is declared, and an
unrecognized declared format SHALL fail closed.

Authored operands SHALL resolve exactly as `release-architecture-forensics`
specifies: literal `HEAD`, a full lowercase-or-uppercase hexadecimal object ID
whose length matches the repository hash format, a fully-qualified `refs/...`
name, or a shorthand looked up only as `refs/tags/<operand>` and
`refs/heads/<operand>`. Shorthand matching both a tag and a head SHALL fail as
ambiguous. Symbolic refs SHALL be dereferenced with cycle detection, annotated
tags SHALL peel recursively, and a final non-commit object SHALL fail closed.
Revision-expression syntax such as `HEAD~2` SHALL NOT be interpreted.

#### Scenario: Default repository and format
- **WHEN** `history analyze --from <a> --to <b>` runs inside a Git working tree
  without `--repository` or `--format`
- **THEN** the enclosing repository is discovered by upward search and the
  versioned canonical JSON report is emitted

#### Scenario: Markdown report
- **WHEN** `history analyze --from <a> --to <b> --format markdown` succeeds
- **THEN** the deterministic human-readable report is written without changing
  the canonical JSON artifact semantics

#### Scenario: Shorthand collision
- **WHEN** both `refs/tags/release` and `refs/heads/release` exist and `--to release` is authored
- **THEN** the command fails with an ambiguous-ref diagnostic and emits no successful report

#### Scenario: Revision expression rejected
- **WHEN** `--from HEAD~2` is authored and no ref with that exact name exists
- **THEN** the command fails with an unresolved-ref diagnostic instead of evaluating ancestry syntax

### Requirement: Canonical object database access
Canonical evidence SHALL be read from the repository object database directly rather
than from a Git executable or a presentation API. The implementation SHALL read
zlib-compressed loose objects and packfiles, resolving packfile `OBJ_OFS_DELTA` and
`OBJ_REF_DELTA` entries against their base objects before use.

Object IDs SHALL be retained canonically as the full digest rendered as lowercase
ASCII hexadecimal with exactly two characters per digest byte, for both SHA-1 and
SHA-256 repositories. A required object that is absent, truncated, or structurally
unreadable SHALL fail analysis closed rather than contribute empty or zero evidence.

#### Scenario: Packed object evidence
- **WHEN** every object in the analyzed range lives in a packfile as a delta against a base object
- **THEN** ingestion produces the same canonical evidence as an equivalent loose-object repository

#### Scenario: Missing required object
- **WHEN** an object required by the analyzed range cannot be read from the object database
- **THEN** ingestion fails closed with an object diagnostic naming that object ID

### Requirement: Canonical evidence and successful report
A successful run SHALL retain one finalized canonical analysis result and emit
exactly one versioned report containing the evidence
`release-architecture-forensics` declares mandatory for interpretability:

- repository object-hash format, authored `from`/`to` operands, and resolved
  lowercase full commit object IDs;
- the analyzed commit set in canonical commit order, each carrying its canonical
  commit ID, exact committer epoch-second integer, raw committer timezone token,
  canonical author identity, ordered lowercase-hexadecimal `encoding ` header
  provenance, and merge status;
- the complete ordered TaskKey match-provenance records and the deduplicated
  canonical TaskKey set per commit;
- the excluded merge count;
- every local exact-rename candidate with its canonical commit ID, source path,
  destination path, blob object ID, lineage-component membership, and
  accepted/`ambiguous_dag` outcome;
- every logical file with its canonical path, aliases, distinct commit count,
  aggregated additions/deletions/churn, and its canonical file events with change
  kind and line-count status.

Canonical JSON report output SHALL use UTF-8 without a byte-order mark, LF line
endings, two-space indentation, no trailing whitespace, exactly one terminal LF,
and exact non-exponent decimal integers for counts, TaskKey identifiers, epoch
seconds, and byte spans. Repeated runs over identical repository objects,
operands, effective configuration, and tool version SHALL produce identical bytes.

An empty analyzed range SHALL succeed and emit a report with zero commits, zero
logical files, a zero excluded merge count, and no candidates.

#### Scenario: Byte-identical repeat run
- **WHEN** the same authored range is analyzed twice from the same repository objects
- **THEN** both runs emit identical canonical JSON report bytes

#### Scenario: Empty range succeeds
- **WHEN** `Reachable(to) \ Reachable(from)` is empty
- **THEN** analysis succeeds with explicit empty evidence rather than failing

#### Scenario: Markdown report view
- **WHEN** `--format markdown` is authored
- **THEN** a deterministic human-readable report of the same finalized evidence
  is written and the canonical JSON artifact semantics remain unchanged

### Requirement: Fail-closed diagnostic surface
Every fail-closed condition SHALL produce a diagnostic carrying a stable diagnostic
kind and, where available, the relevant canonical object ID, canonical path, or raw
message byte span. Stable kinds SHALL at minimum distinguish repository discovery
failure, unsupported object format, unresolved ref, ambiguous ref, ref cycle,
non-commit ref target, missing or unreadable object, malformed commit metadata,
invalid selected author UTF-8, invalid commit-message UTF-8, invalid Git path UTF-8,
and TaskKey span-overlap ambiguity.

Diagnostics SHALL be written to the error stream and SHALL NOT be emitted as
records inside a successful report. A diagnostic raised before publication SHALL
leave every report destination untouched and exit with a non-zero exit code. If a
stream write or independent file rename fails after another destination has
already received a report, the run SHALL instead use the typed `partial-output`
or `output-failed` publication evidence defined below; it SHALL still exit
non-zero and SHALL never claim that the complete report set was published.

#### Scenario: No partial result on failure
- **WHEN** a commit message in the analyzed range is not valid UTF-8
- **THEN** a message-encoding diagnostic is written to the error stream, the
  output stream carries no successful report, and the exit code is non-zero

#### Scenario: TaskKey overlap ambiguity
- **WHEN** two extractor matches claim overlapping raw message byte spans and map to different canonical TaskKeys
- **THEN** ingestion fails closed with an overlap diagnostic identifying the conflicting byte spans

### Requirement: Stable task extractor producer seam
Task extraction SHALL run over the raw commit-message payload bytes through a stable
extractor seam so that #237 can supply configured extractors without changing
ingestion semantics. Every extractor SHALL be identified by a stable extractor ID
matching `[a-z][a-z0-9._-]*` and SHALL produce matches carrying a namespace, a
positive arbitrary-precision decimal identifier, and a non-empty half-open raw
message byte span.

The default effective extractor set SHALL be exactly the `issue` extractor defined by
`release-architecture-forensics`. Extractor output SHALL be deduplicated and ordered
by ascending span start, ascending span end, extractor ID, then canonical TaskKey,
independently of the order in which extractors ran.

#### Scenario: Extractor order independence
- **WHEN** two extractors produce non-overlapping matches in either registration order
- **THEN** the canonical provenance ordering and canonical TaskKey set are identical

#### Scenario: Default extractor boundaries
- **WHEN** a commit message contains `abc#12 #12foo ##12 #12#13 (#14) #001 #0`
- **THEN** the default extractor produces canonical TaskKeys `(issue,14)` and `(issue,1)` only

### Requirement: Deterministic co-change graph evidence

A successful history analysis result SHALL retain a deterministic co-change
projection over the retained logical files. It SHALL expose every canonical
pair association with its ordered endpoint paths, endpoint-category cohort,
commit-evidence IDs, canonical TaskKeys, raw commit and task counts, and whether
the pair is a `G0` edge. A pair is a `G0` edge only when its commit count is
positive; task-only evidence SHALL remain observable but SHALL NOT create a
base-graph edge.

Every `G0` edge SHALL expose its nine-place half-even commit component, task
component, combined co-change weight, cohort-local rank, and the effective
co-change commit/task weights that produced it. Components and ranks SHALL use
only `G0` edges in the same unordered endpoint-category cohort. Graph vertices SHALL retain the
canonical logical-file identity and links to applicable ordered rename-component
provenance. Pair TaskKeys SHALL retain the canonical TaskKey identity used by
the original ordered task provenance.

When `co_change_significance` is configured, the result SHALL expose clusters
formed only from `G0` edges whose already quantized combined weight is greater
than or equal to the threshold. A cluster SHALL contain at least two members,
remain endpoint-cohort-local, sort members by canonical scalar-value path, and
retain only qualifying edges for its maximum and nine-place half-even aggregate.
Without a threshold, the result SHALL retain pair evidence and expose no
clusters.

#### Scenario: Task-only pair remains outside the base graph
- **WHEN** one canonical TaskKey has file episodes for two files but no
  canonical file-evidence commit contains both files
- **THEN** their pair exposes a positive task count and zero commit count but
  is not a `G0` edge

#### Scenario: Threshold-qualified cluster excludes an internal weak edge
- **WHEN** AB has `.600000000`, BC has `.700000000`, AC has `.590000000`, and
  the configured threshold is `.600000000`
- **THEN** the cluster `{A,B,C}` exposes maximum `.700000000` and aggregate
  `1.300000000` from AB and BC only

### Requirement: UTF-8 canonical JSON stdout boundary
When `history analyze` selects `json`, the CLI SHALL emit the successful
canonical report to process standard output as UTF-8 bytes without a byte-order
mark, independently of the host console code page or `TextWriter` encoding.
Markdown and failure diagnostics remain separate text/error surfaces and SHALL
NOT alter successful JSON bytes.

#### Scenario: Non-ASCII JSON stdout
- **WHEN** successful Git evidence contains a non-ASCII canonical path or identity
- **THEN** redirected JSON stdout contains the direct UTF-8 bytes for that scalar and no BOM

### Requirement: Report serialization failure diagnostic
The CLI SHALL fail closed when successful report serialization cannot produce valid output.

If successful report rendering rejects invalid internal Unicode, `history
analyze` SHALL emit a `report_serialization_invalid` diagnostic to standard
error, leave standard output empty, and exit non-zero.

#### Scenario: No report after serialization failure
- **WHEN** canonical report rendering encounters an unpaired surrogate
- **THEN** only the deterministic serialization diagnostic is emitted and no
  JSON report, Markdown report, partial ranking, or candidate set reaches stdout

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
sequence. Every file sink SHALL be staged and validated before publication.
After every sink's content has been produced without error, staged file renames
SHALL be committed before any stream sink (`stdout` or `stderr`) is written;
`stdout` SHALL be written before `stderr` when both are configured. A file
rename failure SHALL leave stream sinks untouched. A collision, file
write/validation error during staging, or the JSON Unicode/serialization
failure already defined for the report-serialization diagnostic SHALL fail
before publication and leave all destinations untouched. A stream-write or
file-rename failure can occur after publication has begun and cannot undo a
stream delivery or an earlier independent rename; in that case the run SHALL
return non-zero and expose a canonical History diagnostic with stable kind
`report_publication_failed`, `publicationStatus` (`partial-output` or
`output-failed`), and destination arrays `failed`, `committed`, `delivered`,
and `uncommitted`. It SHALL also include a boolean `cancelled` and a `details`
array. The CLI SHALL NOT claim to replace multiple independent file
destinations atomically as a set, and SHALL never describe a partially
published run as successful.

JSON written to `stdout` through `--report` SHALL use the same raw UTF-8
without-BOM boundary as the existing `--format json` stdout path. JSON
written to `stderr` or a file destination SHALL use ordinary text writes.

`analyze` SHALL accept `--timings` as an opt-in diagnostic switch. When enabled,
the command SHALL write one stable timing line to standard error containing
policy loading, ingestion, scoring, optional enrichment, JSON rendering,
Markdown rendering, output routing, and the ingestion invocation count. Timing
values SHALL be observational only and SHALL NOT affect report bytes, finding
identity, or exit status.

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

#### Scenario: Commit failure reports partial output honestly
- **WHEN** one file sink is renamed successfully and a later independent
  rename fails, or a stream sink is delivered before a later stream write
  fails
- **THEN** the process exits non-zero with `partial-output` evidence listing
  the delivered/committed and uncommitted destinations, and it does not claim
  that the complete multi-format set was published

#### Scenario: File rename failure precedes stream publication
- **WHEN** one staged file rename fails while stream sinks are configured
- **THEN** no stream sink receives a report, the process exits non-zero, and a
  `report_publication_failed` diagnostic identifies failed, committed,
  delivered, and uncommitted destinations

#### Scenario: Timing evidence distinguishes packed work
- **WHEN** the same explicit Git fixture and effective configuration are run
  once as two single-format CLI processes and once as one packed CLI process
  with `--report json=<path> --report markdown=<path> --timings`
- **THEN** each process reports its phase timings and ingestion invocation count,
  the two-process shape reports two ingestion calls while the packed shape
  reports one, and the measurement harness records wall clock, process
  overhead, renderer phases, and peak working set without claiming an exact
  two-times end-to-end speedup
