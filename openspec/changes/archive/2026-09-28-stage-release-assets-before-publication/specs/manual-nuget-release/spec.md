## MODIFIED Requirements

### Requirement: Release history evidence is durable and candidate-bound

Each successful history job SHALL create `release-forensics.json`, `release-forensics.md`, a compact provenance manifest, checksums, and separate operational observations. The manifest SHALL bind repository, candidate commit/tree/version, selected base tag/SHA when applicable, range and series identity, tool/package identity, history report schema/semantics, effective history-policy identity, and canonical report content digests. Run timestamps and operational measurements SHALL remain outside the canonical history JSON.

Dry-run candidates SHALL upload the complete bundle as workflow artifacts without public writes. For publishing candidates, the existing GitHub Release job SHALL wait for both required acceptance and the complete bundle, attach the verified files, and verify their read-back digests. A newly created GitHub Release SHALL remain a draft until every expected candidate-bound asset is uploaded and its read-back digest is verified; the job SHALL publish the draft only after this verification. An identical candidate/content retry MAY complete successfully; pre-existing conflicting asset content SHALL fail without silent overwrite. A matching draft MAY be resumed by adding missing assets and completing verification. A published immutable release with missing expected assets SHALL fail before upload with an actionable error because its asset inventory cannot be repaired under that tag. A complete published release MAY be verified idempotently. The existing release publication and tag lifecycle SHALL remain the only public publisher.

#### Scenario: Dry-run preserves a reviewable bundle

- **WHEN** a dry-run candidate completes history analysis
- **THEN** JSON, Markdown, manifest, checksums, and operational observations are available as workflow artifacts
- **AND** no public release asset or tag is written

#### Scenario: Publication verifies retained assets

- **WHEN** all required acceptance jobs and history-forensics succeed for a publishing candidate
- **THEN** the existing GitHub Release job creates or resumes a draft, attaches the candidate-bound bundle, and verifies each expected asset's downloaded digest while the release remains a draft
- **AND** it publishes the draft only after every expected asset is present and verified
- **AND** an existing asset with different content causes a visible failure without replacement

#### Scenario: Published immutable release cannot be repaired under the same tag

- **WHEN** a published immutable release is missing an expected candidate-bound asset
- **THEN** the publisher fails before attempting an upload and explains that a new release version is required

#### Scenario: Complete published release retry is idempotent

- **WHEN** a published release already contains every expected asset with matching bytes
- **THEN** the publisher verifies the assets and succeeds without changing the release

#### Scenario: Analysis and orchestration measurements remain separate

- **WHEN** the history bundle is generated
- **THEN** CLI analysis/render phase durations and process memory are recorded separately from orchestration durations and run timestamps
- **AND** neither measurement is included in canonical history JSON or represented as the existing PR-performance KPI
