## Purpose

Define the safety, evidence, and review boundaries for the repository's required
Qodana Community .NET quality gate.

## Requirements

### Requirement: Independent Community quality gate

The repository SHALL run tokenless Qodana Community for .NET against ArchLinterNet.slnx in a separate PR workflow. Its finding-level quality gate SHALL be required independently of the other checks. The scanner SHALL have read-only permissions and no secrets; a trusted reporting step SHALL validate evidence and compare the PR head with a fresh scan of its exact base revision.

#### Scenario: Ordinary or fork pull request
- **WHEN** a pull request is analyzed
- **THEN** the scan runs on an ephemeral hosted runner without secrets or write permissions
- **AND** the head report is compared to a separate scan of the exact base revision
- **AND** a trusted reporter publishes the validated result and a required status for the exact PR head

#### Scenario: Pull request changes Qodana configuration
- **WHEN** a pull request changes scanner configuration or reporting code
- **THEN** those changes cannot weaken the gate evaluating that pull request
- **AND** the trusted reporter uses its default-branch implementation and trusted base-revision configuration

#### Scenario: Pull request refreshes the baseline
- **WHEN** a pull request updates the Qodana baseline
- **THEN** the baseline is the only changed path and the candidate exactly matches the successful SARIF inventory for the unchanged base source, stamped with the base SHA
- **AND** the current verdict compares the head to a fresh base scan; the candidate inventory becomes a reviewed record only after merge
- **AND** a baseline update mixed with source or policy changes fails closed

### Requirement: Fail-visible bounded evidence collection

Each invocation SHALL be bounded and preserve exit status, duration, image identity, cache size and valid SARIF evidence. New actionable findings SHALL fail the required gate, while lower-severity diagnostics remain visible in the report. Missing, malformed or failed analysis SHALL NOT count as clean.

#### Scenario: New actionable findings
- **WHEN** the validated head SARIF contains any High or Critical finding, or a Moderate finding absent from the fresh base scan
- **THEN** the required Qodana status fails
- **AND** the PR report lists finding deltas, severity counts, rule identifiers and source locations

#### Scenario: Scanner failure or missing report
- **WHEN** the scanner fails, times out, or produces no usable SARIF
- **THEN** the required gate fails visibly and reports that the result is unassessable
- **AND** it does not publish a passing status for that PR head

#### Scenario: Stale or mismatched evidence
- **WHEN** evidence does not match the expected workflow, repository, PR number, base revision, head revision, image identity or run attempt
- **THEN** the trusted reporter rejects the evidence and the required gate fails closed

### Requirement: Controlled adoption validation

An opt-in validation run SHALL compare cold/warm findings on the same image and commit and
verify a positive and negative inspection probe outside the canonical solution.

#### Scenario: Inspection or determinism regression

- **WHEN** the expected inspection is missing, remains after its correction, or warm findings differ
- **THEN** validation fails instead of claiming successful adoption

### Requirement: Reviewed promotion

The required Qodana gate SHALL use a reviewed differential policy from the exact PR base revision and a fresh scan of that base. Baseline changes, rule severity changes and suppressions SHALL only take effect for subsequent pull requests after review and SHALL NOT hide findings introduced by the pull request that changes them.

#### Scenario: Insufficient or invalid gate evidence
- **WHEN** runtime, reliability, findings or evidence-integrity checks are incomplete
- **THEN** the required status remains failed or unassessable and the report identifies the missing evidence

#### Scenario: Baseline or rule-policy change in a pull request
- **WHEN** a pull request updates the baseline, scanner configuration or severity policy
- **THEN** the gate evaluates it against the freshly scanned base and policy from its trusted base revision
- **AND** a baseline update is accepted only in a baseline-only PR when it exactly matches the scan of unchanged base source, and becomes a reviewed inventory only after merge

#### Scenario: Insufficient burn-in evidence
- **WHEN** an opt-in lifecycle run lacks a successful cold/warm comparison or positive and negative inspection probes
- **THEN** that run fails instead of claiming successful Qodana adoption
- **AND** incomplete exact-run evidence remains failed or unassessable at the required gate
