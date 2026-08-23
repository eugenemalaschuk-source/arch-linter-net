## MODIFIED Requirements

### Requirement: Versioned successful Release Architecture Forensics report
After canonical Git analysis succeeds, the system SHALL project one successful
`release-architecture-forensics` report with schema version `1`. Its explicit
schema order SHALL begin with schema version, kind, history-semantics version,
and tool version, followed by analysis identity, canonical Git evidence,
findings, enrichment, and candidates.

The canonical JSON composition boundary SHALL own the report kind/version,
deterministic section order, and final canonical text framing. Each independent
analysis, evidence, or optional report section SHALL project its finalized data
through a focused reporting collaborator. The composition boundary and its
collaborators SHALL consume finalized `HistoryIngestionResult`/analysis data
only; they SHALL NOT read Git or policy inputs or recalculate findings.

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

#### Scenario: Sectioned projection preserves the v1 artifact
- **WHEN** a finalized result contains configuration, evidence, findings,
  enrichment, and candidates
- **THEN** focused reporting collaborators project the existing v1 sections in
  the composition boundary's deterministic order with byte-identical canonical
  output
