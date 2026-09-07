## MODIFIED Requirements

### Requirement: Dynamic strict self-policy badge
The repository SHALL expose one primary ArchLinterNet-specific README Architecture Health badge sourced from a stable public Shields endpoint JSON payload. The badge SHALL communicate the independent canonical Gate, canonical Architecture Health, accumulated explicit ignore debt, and effective policy-control count; it SHALL not represent or infer any value from generic workflow success, badge color, test coverage, architecture-coverage percentage, SonarCloud, or Codecov status.

#### Scenario: Primary README badge uses the canonical public payload
- **WHEN** a reader views the README after a verified `main` publication
- **THEN** its Architecture Health image resolves through the stable public endpoint payload rather than `ci.yml` or another workflow-status image
- **AND** the message includes canonical Gate, canonical Health, explicit ignore debt, and effective rule count
- **AND** its navigation provides the current publication receipt and an explanation of evidence and renderer freshness

#### Scenario: Default branch strict self-policy passes
- **WHEN** a pull request with successful required architecture validation is squash-merged and its validated tree matches `main`
- **THEN** the primary README badge renders the promoted canonical Architecture Health payload for that merged tree
- **AND** it does not use a generic strict-policy workflow conclusion as its source

#### Scenario: Strict self-policy fails
- **WHEN** canonical Architecture Health for the promotable pull-request tree is failing
- **THEN** the stable primary badge renders the failing canonical Gate and Health payload
- **AND** it does not present a passing architecture state because another workflow or generic quality signal succeeded

#### Scenario: Generic quality remains separate
- **WHEN** a reader views the README badge block
- **THEN** Main quality, SonarCloud, and Codecov remain separately named generic-quality signals
- **AND** none of them is labeled or described as Architecture Health

### Requirement: Public Architecture Health state is atomically current
The stable public Architecture Health endpoint and its publication receipt SHALL represent one indivisible, current `main` publication. Trusted automation SHALL publish a ready payload only while the push commit remains the current `main` tip. A stale event or replayed workflow SHALL make no publication write. The versioned receipt SHALL bind repository, analyzed base and PR-head commits and tree, merged `main` commit and tree, PR, producer run and attempt, publisher run and attempt, payload digest, publication time, status, and reason. Transport or rendering time SHALL not change the deterministic badge payload or its canonical identity. When evidence is absent, rejected, or unavailable, the endpoint SHALL be replaced with a reviewed CLI-generated `UNASSESSABLE В· ? ignores В· ? rules` payload without requiring a CLI restore, build, or execution at publication time. A failed publication update SHALL leave neither a new payload paired with an old receipt nor old ready data represented as current.

#### Scenario: Receipt proves a ready main publication
- **WHEN** trusted automation promotes valid producer evidence for a squash merge
- **THEN** the receipt binds the matching analyzed PR/base/head and merged main commit/tree identities, producer and publisher attempts, payload digest, and publication time
- **AND** a reader can verify the raw receipt independently of Shields or README rendering

#### Scenario: Fallback remains available when the CLI cannot execute
- **WHEN** trusted publication cannot resolve ready evidence and the runner cannot restore, build, or execute the CLI
- **THEN** the stable endpoint is replaced with the reviewed explicit unassessable payload
- **AND** its receipt records that unavailable state and bounded provenance

#### Scenario: Replayed older main event cannot overwrite the current badge
- **WHEN** a publisher run for an earlier `main` commit reaches its write boundary after `main` has advanced
- **THEN** it makes no update to the public endpoint or receipt
- **AND** the newer publication remains intact

#### Scenario: Payload and metadata cannot become partially current
- **WHEN** a publication write encounters a concurrent change or other write failure
- **THEN** readers observe either the complete previous publication or the complete new publication
- **AND** they do not observe a new payload paired with a previous receipt
