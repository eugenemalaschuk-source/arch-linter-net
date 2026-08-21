# release-forensics-history-cli Specification

## Purpose

Define the shipped `history` command family, the canonical Git ingestion it runs over an
explicit authored range, the minimal deterministic ingestion result it emits, and the
fail-closed diagnostic surface that replaces a result whenever ingestion cannot be
canonically completed. `release-architecture-forensics` remains the semantic authority for
what the evidence means; this capability defines how it is produced and surfaced.
## Requirements
### Requirement: History command family and authored range operands
The shipped CLI SHALL expose a `history` command family whose `ingest` subcommand
runs canonical Release Architecture Forensics Git ingestion over an explicit
authored range.

`ingest` SHALL accept a required `--from` operand, a required `--to` operand, an
optional `--repository` path defaulting to the current directory, and an optional
`--format` selector accepting `json` (default) and `text`. `--from` is exclusive and
`--to` is inclusive.

The repository SHALL be located by walking from the requested path toward the
filesystem root until a Git directory is found, supporting both a `.git` directory
and a `.git` file containing a `gitdir:` pointer. The repository object-hash format
SHALL be read from the repository's own configuration, defaulting to SHA-1 when no
`extensions.objectformat` value is declared, and an unrecognized declared format
SHALL fail closed.

Authored operands SHALL resolve exactly as `release-architecture-forensics`
specifies: literal `HEAD`, a full lowercase-or-uppercase hexadecimal object ID whose
length matches the repository hash format, a fully-qualified `refs/...` name, or a
shorthand looked up only as `refs/tags/<operand>` and `refs/heads/<operand>`.
Shorthand matching both a tag and a head SHALL fail as ambiguous. Symbolic refs
SHALL be dereferenced with cycle detection, annotated tags SHALL peel recursively,
and a final non-commit object SHALL fail closed. Revision-expression syntax such as
`HEAD~2` SHALL NOT be interpreted.

#### Scenario: Default repository and format
- **WHEN** `history ingest --from <a> --to <b>` runs inside a Git working tree without `--repository` or `--format`
- **THEN** the enclosing repository is discovered by upward search and the result is emitted as canonical JSON

#### Scenario: Shorthand collision
- **WHEN** both `refs/tags/release` and `refs/heads/release` exist and `--to release` is authored
- **THEN** the command fails with an ambiguous-ref diagnostic and emits no ingestion result

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

### Requirement: Minimal deterministic ingestion result
A successful run SHALL emit exactly one ingestion result containing the evidence
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

Canonical JSON output SHALL use UTF-8 without a byte-order mark, LF line endings,
two-space indentation, no trailing whitespace, exactly one terminal LF, and exact
non-exponent decimal integers for counts, TaskKey identifiers, epoch seconds, and
byte spans. Repeated runs over identical repository objects, operands, and tool
version SHALL produce identical bytes.

An empty analyzed range SHALL succeed and emit a result with zero commits, zero
logical files, and a zero excluded merge count.

#### Scenario: Byte-identical repeat run
- **WHEN** the same authored range is ingested twice from the same repository objects
- **THEN** both runs emit identical canonical JSON bytes

#### Scenario: Empty range succeeds
- **WHEN** `Reachable(to) \ Reachable(from)` is empty
- **THEN** ingestion succeeds with explicit empty evidence rather than failing

#### Scenario: Text format summary
- **WHEN** `--format text` is authored
- **THEN** a deterministic human-readable summary of the same evidence is written and the canonical JSON result is not required to be printed

### Requirement: Fail-closed diagnostic surface
Every fail-closed condition SHALL produce a diagnostic carrying a stable diagnostic
kind and, where available, the relevant canonical object ID, canonical path, or raw
message byte span. Stable kinds SHALL at minimum distinguish repository discovery
failure, unsupported object format, unresolved ref, ambiguous ref, ref cycle,
non-commit ref target, missing or unreadable object, malformed commit metadata,
invalid selected author UTF-8, invalid commit-message UTF-8, invalid Git path UTF-8,
and TaskKey span-overlap ambiguity.

Diagnostics SHALL be written to the error stream and SHALL NOT be emitted as records
inside an ingestion result. A run that produces a diagnostic SHALL write no
ingestion result to the output stream and SHALL exit with a non-zero exit code.

#### Scenario: No partial result on failure
- **WHEN** a commit message in the analyzed range is not valid UTF-8
- **THEN** a message-encoding diagnostic is written to the error stream, the output stream carries no ingestion result, and the exit code is non-zero

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

A successful history ingestion result SHALL retain a deterministic co-change
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

