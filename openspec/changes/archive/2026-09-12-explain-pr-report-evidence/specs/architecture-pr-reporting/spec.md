## MODIFIED Requirements

### Requirement: Report headline preserves independent governance dimensions
The Markdown headline SHALL directly state architecture acceptance, whether architecture is healthy, debt-bearing, degrading, failing, or unassessable, effective policy control count, control applicability/evaluability, configured topology completeness, explicit waiver debt, existing finding debt, new architecture debt, policy weakening, metric state, and required external-evidence state whenever the canonical evidence configures those dimensions. For every dimension whose canonical state is not `pass`, `not_configured`, or `not_applicable`, the report SHALL render its canonical reason codes and stable supplied identities in a compact Health explanation; it SHALL label only `fail` and `unassessable` reasons as Gate blockers.

Effective policy count, applicability/evaluability, topology mapping, and external-evidence run counts SHALL remain separate disclosure dimensions and SHALL NOT be presented as a combined score, percentage, grade, or quality rating. Finding debt and explicit waiver debt SHALL remain distinct. Optional or not-applicable evidence SHALL remain distinct from unassessable evidence.

#### Scenario: Clean complete PR remains concise
- **WHEN** the artifacts report `gate=pass`, `health=healthy`, zero explicit waiver debt, complete applicability, complete configured topology, and current required external evidence
- **THEN** the Markdown identifies the PR as architecturally acceptable and presents the configured facts without debt or blocking drill-down sections

#### Scenario: Reviewed active waiver debt remains distinct
- **WHEN** the Health artifact reports `gate=pass`, `health=debt`, existing finding debt or only active reviewed waiver debt, and no blocking lifecycle state
- **THEN** the Markdown identifies the non-blocking debt state and separately shows existing findings and explicit waiver counts
- **AND** it does not label the report healthy or collapse the waiver into finding debt

#### Scenario: Degrading waiver metadata has an explanation but is not a blocker
- **WHEN** the Health artifact reports `gate=pass`, `health=degrading`, and the waiver-debt dimension has `metadata_incomplete` as its canonical reason
- **THEN** the Health explanation identifies that reason and its supplied identity as non-blocking debt
- **AND** the report does not call that advisory reason a Gate blocker

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
The Markdown SHALL show blockers before non-blocking debt and SHALL provide bounded drill-down for new or broadened waiver weakening, stale, expired, invalid, or metadata-incomplete waivers using canonical lifecycle identity, rule, target, reason, owner, issue, expiry, and provenance where supplied. It SHALL render the full canonical lifecycle-count breakdown whenever waiver inventory is available, including `metadata_incomplete` and `invalid`, and use supplied remediation categories exactly; it SHALL not invent fixes.

For active-only reviewed waiver debt, compact aggregate disclosure is sufficient. For unavailable evidence or non-zero blocker/debt/change detail, the report SHALL provide stable navigation references to the canonical Health, policy-inventory/waiver, change, normalized-finding/remediation, applicability, topology, and external-evidence artifacts where the supplied evidence contains them. A validated explicit transport navigation context may add only safe, repository/run/head-bound immutable report-bundle or workflow URLs; it SHALL not change canonical Gate, Health, evidence availability, or rendered semantics. Such full-report navigation SHALL remain visible regardless of ordinary detail truncation.

#### Scenario: New waiver weakening is a blocking governance change
- **WHEN** the supplied Health artifact reports a new or broadened waiver as canonical policy-weakening or new-debt gate evidence under the default strict profile
- **THEN** the Markdown renders it before ordinary debt as a blocking governance change
- **AND** it does not reinterpret the change as neutral waiver inventory growth

#### Scenario: Expired or stale waiver preserves lifecycle semantics
- **WHEN** supplied canonical lifecycle evidence contains an expired, invalid, stale, or metadata-incomplete waiver
- **THEN** the Markdown renders the canonical state, full aggregate lifecycle breakdown, and available lifecycle metadata in bounded detail
- **AND** its headline follows the supplied Health gate and health state rather than treating the waiver as harmless active debt

#### Scenario: Full report navigation survives bounded findings
- **WHEN** canonical navigation contains more ordinary entries than the configured detail bound and the producer supplies a validated run-bound report-bundle URL
- **THEN** the report retains the deterministic ordinary showing and omitted counts
- **AND** it displays the report-bundle URL outside that bounded ordinary navigation list

#### Scenario: Blocking findings reuse remediation guidance
- **WHEN** supplied canonical finding evidence contains a remediation category
- **THEN** the Markdown renders that category and its supplied concise guidance with the finding
- **AND** it does not generate a new remediation instruction
