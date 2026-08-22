# release-forensics-reporting Specification

## Purpose

Define the versioned successful Release Architecture Forensics report: its
canonical JSON bytes, deterministic Markdown reading view, optional enrichment
boundary, evidence-backed candidate investigations, and separate fail-closed
diagnostics. The report consumes finalized canonical Git evidence and never
reinterprets or recomputes upstream analysis semantics.
## Requirements
### Requirement: Versioned successful Release Architecture Forensics report
After canonical Git analysis succeeds, the system SHALL project one successful
`release-architecture-forensics` report with schema version `1`. Its explicit
schema order SHALL begin with schema version, kind, history-semantics version,
and tool version, followed by analysis identity, canonical Git evidence,
findings, enrichment, and candidates.

Analysis identity SHALL retain repository object format; authored and resolved
range operands; analyzed commit and excluded merge counts; and the complete
effective `history_analysis` configuration in deterministic order. The report
SHALL retain every finalized upstream commit, TaskKey provenance, rename
candidate/component, logical-file/event, hotspot, co-change, bottleneck, and
OCP evidence without re-resolving or recomputing it.

#### Scenario: Git-only successful report
- **WHEN** finalized canonical Git analysis succeeds without requesting enrichment
- **THEN** the report contains all Git-level evidence and an explicit
  `not_requested` enrichment projection

### Requirement: Canonical JSON report bytes
The JSON report SHALL use `CanonicalJsonWriter` semantics: valid Unicode scalars
only, UTF-8 without BOM, LF line endings, two-space indentation, no trailing
whitespace, exactly one terminal LF, versioned property order, and scalar-value
ordering for dynamic keys. Raw counts, TaskKey IDs, epoch seconds, and gaps
SHALL be exact non-exponent decimal integers; canonical real values SHALL have
exactly nine fractional digits and no exponent notation.

Canonical JSON identity SHALL be over exact bytes. Markdown selection or host
locale, timezone, process state, input enumeration, and host date ranges SHALL
NOT alter JSON bytes for the same finalized evidence, configuration, and tool
version.

#### Scenario: Canonical byte repeat
- **WHEN** one successful finalized input is rendered twice with different host
  locale or timezone settings
- **THEN** both JSON byte sequences are identical

### Requirement: Deterministic evidence-backed candidate records
The report SHALL emit candidates only from finalized findings. Each candidate
SHALL have a stable kind and ID, source finding/evidence IDs, affected canonical
paths or cluster members, exact qualifying score/threshold/cohort/components,
deterministically ordered supporting evidence, and caveat IDs.

A hotspot, bottleneck, or OCP candidate qualifies only when its canonical score
is greater than `0.000000000`. A co-change cluster candidate qualifies only from
an existing canonical `Gtheta` cluster and SHALL retain the configured threshold
and qualifying edges. Candidates SHALL be ordered by candidate kind and their
canonical source-finding order. They are investigations and SHALL NOT assert
formal OCP, coupling, ownership, or refactoring proof.

#### Scenario: No qualifying candidate
- **WHEN** all file scores are canonical zero and no `Gtheta` cluster exists
- **THEN** the report contains an explicit empty candidate array

### Requirement: Reserved optional enrichment projection
Every successful report SHALL contain exactly one versioned enrichment projection
with a deterministic status of `not_requested`, `not_applicable`, `available`,
or `unavailable`. An unavailable projection SHALL retain a bounded deterministic
reason and ordered provenance when supplied. An available projection SHALL be
able to retain deterministic context entries.

Changing only the enrichment projection SHALL NOT change any Git-level evidence,
finding, score, rank, candidate qualification, or candidate ordering.

#### Scenario: Unavailable enrichment preserves Git output
- **WHEN** Git analysis succeeds but optional enrichment cannot produce trusted
  context
- **THEN** the successful report has `unavailable` enrichment and preserves all
  Git-level report evidence and candidates

### Requirement: Deterministic Markdown report and interpretation limits
The system SHALL render the same successful report input as Markdown with
identity/configuration, categorized hotspots, co-change cohorts, bottlenecks,
OCP pressure, candidates, enrichment, and interpretation limits. Markdown SHALL
state that churn is change volume rather than complexity; co-change is not
ownership proof; accepted exact renames have zero content churn; ambiguous or
lifecycle-broken candidates remain ordinary evidence; pathname reuse can
conflate generations; binary/non-line events have zero line churn with status;
merge deltas are excluded; exact rename misses rename-with-edit; TaskKeys
normalize source spellings; normalized scores are cohort-local; role hints and
candidates need human review; and enrichment is optional context.

#### Scenario: Markdown does not change canonical JSON
- **WHEN** the same successful analysis is rendered as Markdown and JSON
- **THEN** the JSON output remains the canonical report artifact and is not
  changed by Markdown rendering

### Requirement: Separate deterministic failure diagnostics
If canonical analysis fails, the system SHALL write no successful report,
partial ranking, or candidate set. It SHALL instead retain the deterministic
diagnostic surface with its stable kind and available object, path, and span
identity. Diagnostic bytes are a separate error surface and SHALL NOT be
treated as successful report bytes.

#### Scenario: Fail-closed analysis
- **WHEN** invalid selected UTF-8 causes canonical analysis to fail
- **THEN** the command writes only the deterministic diagnostic and no
  successful Markdown or JSON report
