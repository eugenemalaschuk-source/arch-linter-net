## ADDED Requirements

### Requirement: Candidate-bound release history analysis
The manual release workflow SHALL run a standalone, read-only history-forensics job after preparing the immutable candidate. The job SHALL verify and use the exact candidate CLI package and candidate source commit/tree, read the complete required Git object graph, and run exactly one Git-only analysis that emits canonical JSON and Markdown from the same finalized result. It SHALL NOT build the analyzed solution or run automatic .NET enrichment.

Stable candidates SHALL use the highest lower stable SemVer release tag whose peeled commit is an ancestor of the candidate. Preview candidates SHALL have a distinct `preview:X.Y.Z` series identity and use the highest lower preview tag in that exact line when one exists; the first preview in a line SHALL use the highest lower stable ancestor. Stable selection SHALL ignore preview and development-build tags. Selection SHALL pin the authored tag and resolved full SHA before analysis and SHALL fail closed for shallow history, missing required objects, ambiguous semantic-version tags, invalid candidate identity, or mismatched candidate package identity. If no eligible predecessor exists, the result SHALL be typed `not-applicable: no_previous_release`, with no fabricated root predecessor or empty successful analysis.

The selected range SHALL be the complete exclusive-base/inclusive-candidate Git range. It SHALL NOT be truncated to a fixed commit count, changed to a date range, or reduced to first-parent history.

#### Scenario: Stable candidate uses the previous stable ancestor
- **WHEN** a stable candidate has lower stable, preview, and `main.N` tags reachable from its commit
- **THEN** the analysis starts exclusively at the highest lower stable tag on candidate ancestry and ends inclusively at the exact candidate commit
- **AND** preview and development-build tags do not become the stable boundary

#### Scenario: Preview candidate remains in its own line
- **WHEN** a preview candidate has a lower preview tag in the same `X.Y.Z-preview.N` line on candidate ancestry
- **THEN** the analysis uses that preview tag as its exclusive base and records series identity `preview:X.Y.Z`
- **WHEN** no prior preview in that line exists
- **THEN** the analysis uses the highest lower stable ancestor, if present

#### Scenario: First release has no predecessor
- **WHEN** no eligible stable predecessor exists for a candidate
- **THEN** the workflow produces a typed `not-applicable: no_previous_release` result
- **AND** it does not invent a root predecessor or claim a successful empty-range analysis

#### Scenario: Incomplete or ambiguous Git identity
- **WHEN** the checkout is shallow, a required tag/object is missing, multiple eligible tags identify the same SemVer version, or candidate/package identity differs
- **THEN** the history job fails and no successful forensics bundle is published

#### Scenario: Findings do not gate release
- **WHEN** a complete history analysis contains high hotspots, bottlenecks, or OCP findings
- **THEN** those findings remain evidence and do not alter release authorization
- **WHEN** range selection, ingestion, rendering, or required artifact production fails
- **THEN** the job fails visibly and package publication remains blocked

### Requirement: Release history evidence is durable and candidate-bound
Each successful history job SHALL create `release-forensics.json`, `release-forensics.md`, a compact provenance manifest, checksums, and separate operational observations. The manifest SHALL bind repository, candidate commit/tree/version, selected base tag/SHA when applicable, range and series identity, tool/package identity, history report schema/semantics, effective history-policy identity, and canonical report content digests. Run timestamps and operational measurements SHALL remain outside the canonical history JSON.

Dry-run candidates SHALL upload the complete bundle as workflow artifacts without public writes. For publishing candidates, the existing GitHub Release job SHALL wait for both required acceptance and the complete bundle, attach the verified files, and verify their read-back digests. An identical candidate/content retry MAY complete successfully; pre-existing conflicting asset content SHALL fail without silent overwrite. The existing release publication and tag lifecycle SHALL remain the only public publisher.

#### Scenario: Dry-run preserves a reviewable bundle
- **WHEN** a dry-run candidate completes history analysis
- **THEN** JSON, Markdown, manifest, checksums, and operational observations are available as workflow artifacts
- **AND** no public release asset or tag is written

#### Scenario: Publication verifies retained assets
- **WHEN** all required acceptance jobs and history-forensics succeed for a publishing candidate
- **THEN** the existing GitHub Release job attaches the candidate-bound bundle and verifies each downloaded asset digest
- **AND** an existing asset with different content causes a visible failure without replacement

#### Scenario: Analysis and orchestration measurements remain separate
- **WHEN** the history bundle is generated
- **THEN** CLI analysis/render phase durations and process memory are recorded separately from orchestration durations and run timestamps
- **AND** neither measurement is included in canonical history JSON or represented as the existing PR-performance KPI
