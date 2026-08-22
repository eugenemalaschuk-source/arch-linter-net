## MODIFIED Requirements

### Requirement: Release forensics module boundaries are self-governed
The repository self-policy SHALL declare and exercise explicit namespace layers
for the Release Architecture Forensics canonical utility, Git ingestion,
configuration, task extraction, canonical file-evidence construction, scoring,
reporting, optional .NET enrichment, and CLI History command modules. Existing
strict dependency contracts SHALL ensure raw Git ingestion cannot depend on
evidence, scoring, reporting, or enrichment; evidence construction cannot
depend on scoring, reporting, or enrichment; scoring may consume finalized
evidence but cannot read raw Git ingestion, render reports, or use enrichment;
optional enrichment cannot depend on report rendering; report rendering cannot
depend on raw Git ingestion; and the CLI History command cannot import internal
Git, configuration, task, evidence, scoring, canonical-utility, or enrichment
modules.

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

#### Scenario: Scoring cannot reach back into evidence construction
- **WHEN** a production canonical-evidence construction type imports a History
  scoring namespace
- **THEN** the strict self-policy reports a violation
- **AND** a scorer may import the finalized History evidence namespace without
  importing raw Git-ingestion types

#### Scenario: History command bypass is introduced
- **WHEN** a CLI History command type imports a History scoring, evidence, Git,
  configuration, task, canonical-utility, or enrichment implementation type
- **THEN** the strict self-policy reports a violation rather than allowing the
  CLI to bypass the reusable History composition/result seam
