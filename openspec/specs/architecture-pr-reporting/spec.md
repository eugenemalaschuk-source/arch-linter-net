# architecture-pr-reporting Specification

## Purpose
Provide an architecture-only pull-request Markdown projection from canonical ArchLinterNet governance artifacts, so reviewers can inspect architecture acceptance, completeness, debt, and change without a second evaluator or CI-script-owned semantics.

## Requirements

### Requirement: PR report is a Core-owned projection of canonical artifacts
The system SHALL define a versioned report-input and projection contract that consumes an `architecture-health/v1` artifact with its canonical report evidence and an `architecture-change` report artifact. It SHALL derive report status directly from the Health gate and health state and SHALL consume policy inventory, waiver lifecycle, applicability, topology, external-evidence, finding, remediation, and change facts only from the supplied canonical artifact evidence.

The projection SHALL NOT load policy YAML, scan projects or assemblies, read SARIF, evaluate waiver dates, recount controls or subjects, infer historical evaluability, resolve Health precedence, or query GitHub, CI checks, or external services. Missing, invalid, stale, wrong-context, ambiguous, or incomplete required report evidence SHALL remain explicitly unavailable or unassessable; the projection SHALL NOT fabricate a passing status or zero rules, waivers, findings, subjects, or evidence runs.

#### Scenario: Complete canonical artifacts produce a review projection
- **WHEN** a valid Health artifact contains complete canonical reporting evidence and a compatible architecture-change artifact is supplied
- **THEN** Core produces one deterministic PR-report projection whose acceptance status equals the supplied Health gate and health state
- **AND** all headline and drill-down facts retain their owning artifact evidence rather than being recomputed

#### Scenario: Legacy or incomplete evidence is not clean
- **WHEN** a supplied Health artifact omits required report evidence or identifies an unassessable, stale, or wrong-context authority
- **THEN** the projection marks the affected evidence and architecture review as unavailable or unassessable
- **AND** it does not render a pass or synthesized zero count for that missing authority

### Requirement: Native CLI renders deterministic bounded architecture Markdown
The CLI SHALL expose a local first-class `report pr` command that accepts explicit Health and architecture-change artifact paths and writes the deterministic architecture PR Markdown to standard output or an explicit output path. Invalid paths, malformed artifacts, unsupported artifact versions, or incompatible artifacts SHALL fail closed with the established CLI error contract.

The rendered Markdown SHALL be architecture-only and SHALL contain no generic build, test, quality, security-service, job, check, or vendor status aggregation. It SHALL use stable ordering and configurable bounded detail expansion suitable for pull-request comments; truncation SHALL preserve counts, canonical identities, and omitted-detail indicators.

#### Scenario: Report is generated outside GitHub Actions
- **WHEN** a developer runs `arch-linter-net report pr` with canonical local Health and change artifacts
- **THEN** the CLI writes the same deterministic Markdown that a publication workflow can consume
- **AND** no GitHub API permission, workflow script report composition, or external analyzer run is required

#### Scenario: Oversized detail remains transparent
- **WHEN** canonical blocking findings, lifecycle records, topology subjects, or evidence receipts exceed the configured report detail bound
- **THEN** the Markdown retains a stable top-N set and an omitted-count indicator for each bounded section
- **AND** the report retains the associated complete canonical artifact identities for navigation

### Requirement: Report headline preserves independent governance dimensions
The Markdown headline SHALL directly state architecture acceptance, whether architecture is healthy, debt-bearing, degrading, failing, or unassessable, effective policy control count, control applicability/evaluability, configured topology completeness, explicit waiver debt, existing finding debt, new architecture debt, policy weakening, metric state, and required external-evidence state whenever the canonical evidence configures those dimensions.

Effective policy count, applicability/evaluability, topology mapping, and external-evidence run counts SHALL remain separate disclosure dimensions and SHALL NOT be presented as a combined score, percentage, grade, or quality rating. Finding debt and explicit waiver debt SHALL remain distinct. Optional or not-applicable evidence SHALL remain distinct from unassessable evidence.

#### Scenario: Clean complete PR remains concise
- **WHEN** the artifacts report `gate=pass`, `health=healthy`, zero explicit waiver debt, complete applicability, complete configured topology, and current required external evidence
- **THEN** the Markdown identifies the PR as architecturally acceptable and presents the configured facts without debt or blocking drill-down sections

#### Scenario: Reviewed active waiver debt remains distinct
- **WHEN** the Health artifact reports `gate=pass`, `health=debt`, existing finding debt or only active reviewed waiver debt, and no blocking lifecycle state
- **THEN** the Markdown identifies the non-blocking debt state and separately shows existing findings and explicit waiver counts
- **AND** it does not label the report healthy or collapse the waiver into finding debt

#### Scenario: Incomplete applicability remains explicit
- **WHEN** one of N required applicability controls is unassessable
- **THEN** the Markdown shows `N-1/N evaluable`, identifies the unassessable control with canonical reason and provenance in bounded detail, and does not render applicability as complete

#### Scenario: Incomplete topology remains explicit
- **WHEN** configured topology evidence identifies unmapped or ambiguous required subjects
- **THEN** the Markdown identifies topology mapping as incomplete and renders bounded canonical subject detail
- **AND** it does not represent unmapped or ambiguous subjects as a green mapping ratio

#### Scenario: Wrong-context external evidence remains unassessable
- **WHEN** required external evidence is validly parsed but is bound to another revision or scope
- **THEN** the Markdown identifies that logical evidence as wrong-context or unassessable
- **AND** it does not present the evidence as current or zero-clean

### Requirement: Report drill-down preserves canonical lifecycle, change, and remediation evidence
The Markdown SHALL show blockers before non-blocking debt and SHALL provide bounded drill-down for new or broadened waiver weakening, stale, expired, invalid, or metadata-incomplete waivers using canonical lifecycle identity, rule, target, reason, owner, issue, expiry, and provenance where supplied. It SHALL render canonical architecture-change movement and use supplied remediation categories exactly; it SHALL not invent fixes.

For active-only reviewed waiver debt, compact aggregate disclosure is sufficient. For unavailable evidence or non-zero blocker/debt/change detail, the report SHALL provide stable navigation references to the canonical Health, policy-inventory/waiver, change, normalized-finding/remediation, applicability, topology, and external-evidence artifacts where the supplied evidence contains them.

#### Scenario: New waiver weakening is a blocking governance change
- **WHEN** the supplied Health artifact reports a new or broadened waiver as canonical policy-weakening or new-debt gate evidence under the default strict profile
- **THEN** the Markdown renders it before ordinary debt as a blocking governance change
- **AND** it does not reinterpret the change as neutral waiver inventory growth

#### Scenario: Expired or stale waiver preserves lifecycle semantics
- **WHEN** supplied canonical lifecycle evidence contains an expired, invalid, stale, or metadata-incomplete waiver
- **THEN** the Markdown renders the canonical state and available lifecycle metadata in bounded detail
- **AND** its headline follows the supplied Health gate and health state rather than treating the waiver as harmless active debt

#### Scenario: Blocking findings reuse remediation guidance
- **WHEN** supplied canonical finding evidence contains a remediation category
- **THEN** the Markdown renders that category and its supplied concise guidance with the finding
- **AND** it does not generate a new remediation instruction
