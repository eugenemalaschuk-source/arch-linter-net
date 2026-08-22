## Why

The completed Git-only history pipeline produces deterministic evidence, but its
`history ingest` surface is still an internal ingestion view rather than the
versioned, human-readable and byte-canonical Release Architecture Forensics
report required by #243. Users cannot yet receive refactoring investigations or
an explicit enrichment state without reconstructing report semantics themselves.

## What Changes

- Add a versioned successful Release Architecture Forensics report with
  byte-canonical JSON and deterministic Markdown renderings from finalized
  Git-only evidence.
- Include the complete upstream evidence, effective `history_analysis`
  configuration identity, hotspot/co-change/bottleneck/OCP findings, a reserved
  enrichment projection, and deterministically ordered refactoring candidates.
- Replace the internal `history ingest` output surface with `history analyze`
  and its `json` (default) and `markdown` report formats. Successful reporting
  is available when enrichment is not requested, not applicable, or unavailable.
- Preserve the fail-closed boundary: a canonical-analysis failure writes only a
  deterministic diagnostic and never a partial report or candidate set.
- Document evidence interpretation and known v1 limitations so candidates are
  clearly investigative heuristics, not design-law conclusions.

## Capabilities

### New Capabilities

- `release-forensics-reporting`: Versioned successful-report schema, canonical
  JSON/Markdown rendering, enrichment status projection, refactoring candidates,
  and deterministic error diagnostics.

### Modified Capabilities

- `release-forensics-history-cli`: Promote the history command from its minimal
  ingestion result to the report-producing `analyze` subcommand and formats.

## Impact

- Core history reporting adds a report projection and retains the existing
  ingestion pipeline as its only evidence producer; it does not add a Git,
  scoring, or Roslyn subsystem.
- CLI command definitions, help text, and focused CLI tests move to the
  `history analyze` report surface.
- Core reporting tests add canonical-byte, candidate, Markdown, enrichment, and
  fail-closed coverage; release-forensics documentation gains report guidance.
