## ADDED Requirements

### Requirement: Release forensics module boundaries are self-governed
The repository self-policy SHALL declare and exercise explicit namespace layers
for the Release Architecture Forensics canonical utility, Git ingestion,
configuration, task extraction, evidence/scoring, reporting, optional .NET
enrichment, and CLI History command modules. Existing strict dependency
contracts SHALL ensure raw Git ingestion cannot depend on analysis, reporting,
or enrichment; evidence/scoring cannot depend on reporting or enrichment;
optional enrichment cannot depend on report rendering; report rendering cannot
depend on raw Git ingestion; and the CLI History command cannot import internal
Git, configuration, task, analysis, canonical-utility, or enrichment modules.

The parent History namespace MAY remain the composition seam coordinating the
finalized reusable result. The policy SHALL include every new rule ID in the
existing rule-input coverage contract; it SHALL not add a test-only policy or a
new contract family for this purpose.

#### Scenario: Report rendering remains independent of Git ingestion
- **WHEN** a production report-rendering type is scanned by the repository
  self-policy
- **THEN** importing a raw Git-ingestion namespace is a strict architecture
  violation while consuming finalized evidence through the reusable History
  result remains allowed

#### Scenario: History command bypass is introduced
- **WHEN** a CLI History command type imports a History analysis, Git,
  configuration, task, canonical-utility, or enrichment implementation type
- **THEN** the strict self-policy reports a violation rather than allowing the
  CLI to bypass the reusable History composition/result seam
