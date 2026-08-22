## ADDED Requirements

### Requirement: Enrichment is a non-authoritative downstream projection
Optional .NET/Roslyn enrichment SHALL execute only after canonical Git-level
analysis has completed. Enabling, disabling, succeeding, or failing enrichment
SHALL NOT change canonical ref/metadata/TaskKey/path/rename/temporal/graph
evidence, finding identity, score, rank, candidate eligibility, or candidate
ordering. A valid Git-only result SHALL remain reportable without enrichment.

#### Scenario: Enrichment failure preserves canonical evidence
- **WHEN** a completed canonical Git analysis cannot obtain .NET enrichment
- **THEN** the same findings, provenance, scores, ranks, and ordering are retained with only the enrichment projection/status differing

#### Scenario: Enrichment cannot repair path identity
- **WHEN** finalized Git evidence contains same-path reuse or an `ambiguous_dag` rename component
- **THEN** enrichment does not split, merge, or otherwise modify the finalized logical-file identity
