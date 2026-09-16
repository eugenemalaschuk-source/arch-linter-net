## ADDED Requirements

### Requirement: Independent advisory Community analysis

The repository SHALL run tokenless Qodana Community for .NET against ArchLinterNet.slnx in a
separate PR workflow. The initial check SHALL NOT be required and SHALL NOT alter other checks.

#### Scenario: Ordinary or fork pull request

- **WHEN** a pull request is analyzed
- **THEN** the job uses an ephemeral hosted runner, read-only repository permissions and no secrets
- **AND** analysis uses committed configuration without a blanket baseline or exclusions
- **AND** reports are uploaded as inert evidence without PR or security-event write permissions

### Requirement: Fail-visible bounded evidence collection

Each invocation SHALL be bounded and preserve exit status, duration, image identity, cache
size and valid SARIF evidence. Missing, malformed or failed analysis SHALL NOT count as clean.

#### Scenario: Scanner failure or missing report

- **WHEN** the scanner fails, times out, or produces no usable SARIF
- **THEN** evidence records the failure and the optional check fails visibly
- **AND** existing required checks remain independent

### Requirement: Controlled adoption validation

An opt-in validation run SHALL compare cold/warm findings on the same image and commit and
verify a positive and negative inspection probe outside the canonical solution.

#### Scenario: Inspection or determinism regression

- **WHEN** the expected inspection is missing, remains after its correction, or warm findings differ
- **THEN** validation fails instead of claiming successful adoption

### Requirement: Reviewed promotion

The initial implementation SHALL remain advisory until representative evidence is reviewed.
Baselines and rule suppressions SHALL only be changed through explicit review.

#### Scenario: Insufficient burn-in evidence

- **WHEN** runtime, reliability, findings or fork evidence is incomplete
- **THEN** documentation records pending acceptance under #864 and does not claim promotion
