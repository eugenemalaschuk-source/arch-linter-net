# governance-applicability-evidence Specification

## Purpose
Define deterministic control-level applicability evidence for v0.8 governance
families without conflating proven evaluability with policy configuration or
architecture quality.
## Requirements
### Requirement: Effective controls have explicit applicability membership
The effective-policy/control projection SHALL materialize one canonical expected
applicability-membership entry for every effective v0.8 control whose family
has applicability semantics. An entry SHALL contain the canonical
effective-control identity, family discriminator, membership (`required`,
`optional`, or `not_applicable`), and canonical policy/family provenance that
established the membership. The expected membership collection SHALL be
complete and independent from produced applicability records.

Every produced applicability record SHALL reference exactly one expected entry
by canonical effective-control identity. Membership belongs to the expected
entry; a record SHALL NOT define a competing membership value. For every
expected entry, the produced-record collection SHALL contain zero or exactly
one record with that identity; duplicate records are invalid
contract-integrity evidence. Before deriving a joined projection or summary, a
consumer SHALL validate the entire produced-record collection, including an
anti-join that proves every produced identity exists in the expected collection.
An unknown or orphan produced identity is invalid contract-integrity evidence;
it SHALL NOT be discarded because it has no left-join row. The expected
membership collection SHALL remain available even when evaluation fails to
produce a record. The collection is an applicability authority, not a second
effective-policy inventory or counting engine; #685 may consume the same
canonical control identity but does not derive membership or a denominator.

One effective control has one expected entry regardless of source-set
expansion, match/finding count, display name, YAML position, or runtime
enumeration. Controls outside the required set SHALL remain explicitly visible
as `optional` or `not_applicable`; they SHALL NOT be silently omitted.

#### Scenario: Required control is counted exactly once
- **WHEN** one effective control is expanded across multiple source sets or
  yields multiple findings
- **THEN** the expected membership collection has exactly one entry for its
  canonical control identity and contributes exactly one member when its
  membership is `required`

#### Scenario: Required record is missing
- **WHEN** expected applicability controls contain required controls A and B,
  produced records contain a record only for A, and B's record is absent
- **THEN** a consumer left joins the expected collection to records, retains A
  and B in the required denominator, and represents B with an unassessable
  `missing_applicability_record` integrity outcome rather than inferring
  membership from family semantics or omitting B

#### Scenario: Duplicate record is unassessable
- **WHEN** two produced applicability records reference the same expected
  required control identity
- **THEN** consumers preserve the control once in the required denominator,
  do not count either duplicate as evaluable, and represent the control with
  an unassessable integrity outcome and duplicate-record provenance

#### Scenario: Orphan record is visible to collection validation
- **WHEN** expected applicability controls contain required control A and
  produced records contain valid A plus a record for unknown control X
- **THEN** the produced-to-expected anti-join retains X as
  `unknown_applicability_record_identity` integrity evidence and the collection
  cannot be reported as complete solely because A joins successfully

#### Scenario: Optional control is visible without inflating the denominator
- **WHEN** explicit policy semantics make configured control C optional
- **THEN** C has an explicit expected `optional` membership entry, remains
  separately disclosed whether its record is evaluable, not applicable, or
  unassessable, and does not inflate the required denominator

### Requirement: Applicability state is typed and non-inferential
Each valid produced applicability record SHALL expose exactly one typed state:
`evaluable`, `not_applicable`, or `unassessable`. State validity is determined
only by its referenced expected membership and evidence condition:

| Expected membership | Evidence condition | Permitted state | Summary treatment |
| --- | --- | --- | --- |
| `required` | All required evidence is sufficient and complete | `evaluable` | Included in the required denominator and evaluable numerator |
| `required` | Required evidence is missing, unexpectedly empty, incomplete, malformed, stale, unmapped, ambiguous, or has invalid trust binding | `unassessable` | Included in the required denominator and unassessable count |
| `optional` | Evidence is deliberately omitted under explicit optional policy semantics | `not_applicable` | Disclosed separately; not in the required denominator |
| `optional` | Supplied evidence is sufficient and complete | `evaluable` | Disclosed separately; not in the required denominator |
| `optional` | Supplied evidence is incomplete, malformed, stale, ambiguous, or otherwise insufficient | `unassessable` | Disclosed separately as insufficient supplied evidence; not treated as optional absence |
| `not_applicable` | The control has no applicability evaluation under explicit policy/family semantics | `not_applicable` | Disclosed separately; not in the required denominator |

The matrix applies only to exactly one compatible produced record. It does not
permit an invalid record or an absent record to be represented as a new valid
membership/state combination. A joined per-control projection SHALL represent
the valid produced-record state separately from its collection-integrity
outcome. Missing, duplicate, unknown-identity, or membership/state-incompatible
records SHALL have an `unassessable` integrity outcome with stable provenance
and no valid produced-record state. A missing `required` record SHALL always
remain in the denominator; an `optional` or `not_applicable` entry with an
unassessable integrity outcome remains outside that denominator but SHALL be
separately disclosed and SHALL NOT be treated as a valid optional absence or
non-applicability outcome.

No other membership/state combination is valid. In particular,
`required`/`not_applicable`, `not_applicable`/`evaluable`, and
`not_applicable`/`unassessable` SHALL NOT be interpreted as valid produced
record outcomes. Zero findings, a zero measured value, an empty result
collection, a configured control count, or a missing record SHALL NOT be
inferred to mean `evaluable`.

#### Scenario: Empty successful result remains evaluable only with proof
- **WHEN** a required control produces zero findings or a neutral measurement
- **THEN** it is `evaluable` only if its record proves the required input and
  family evidence were complete; otherwise it is `unassessable`

#### Scenario: Missing record cannot improve a summary
- **WHEN** an expected entry has required membership but its produced
  applicability record is absent
- **THEN** consumers preserve the expected entry in the required denominator
  and represent its join integrity as unassessable with a
  `missing_applicability_record` reason rather than inferring a complete result

#### Scenario: Missing not-applicable record is integrity evidence, not a state
- **WHEN** an expected entry has membership `not_applicable` and its produced
  applicability record is absent
- **THEN** the joined projection has no valid produced-record state and exposes
  an unassessable `missing_applicability_record` integrity outcome, rather than
  interpreting `not_applicable`/`unassessable` as a valid record state

#### Scenario: Optional supplied evidence is invalid
- **WHEN** an optional external-evidence control receives a supplied SARIF
  artifact that is malformed or bound to the wrong revision
- **THEN** its record is `unassessable`, not `not_applicable`, and its
  provenance identifies the insufficient supplied evidence

#### Scenario: Not-applicable control cannot become evaluable
- **WHEN** an expected entry has membership `not_applicable` and a produced
  record claims `evaluable` or `unassessable`
- **THEN** the result is invalid contract-integrity evidence and consumers do
  not interpret the record as a valid applicability outcome

### Requirement: Unassessable state preserves stable reason and provenance
A valid `unassessable` record or an unassessable joined collection-integrity outcome SHALL contain one or more deterministic reason classes and canonical
provenance sufficient to identify the insufficient evidence. Every reason on a
valid `unassessable` produced record SHALL have the same family, canonical
effective-control identity, and policy identity as that record's canonical
provenance. A record with no reason or with mismatched reason provenance is
invalid contract-integrity evidence and SHALL be represented by canonical
invalid-record-integrity evidence rather than exposing the untrusted reason.
Supported reason classes SHALL distinguish, where meaningful to the family,
missing or unavailable required input, unexpected empty input, unmapped
subject, ambiguous subject, stale declaration, malformed or failed external
input, wrong external evidence identity, repository, revision, or scope,
`missing_applicability_record`, duplicate applicability-record identity,
`unknown_applicability_record_identity`, and incompatible applicability-record
identity or state.

Families MAY define additional bounded reason classes but SHALL NOT use display
text as the machine-readable reason.

#### Scenario: Stale and missing evidence stay distinguishable
- **WHEN** one control has a declared selector with no current match and another
  has no produced applicability record
- **THEN** their results expose distinct stable reason classes and provenance
  rather than a shared zero-result status

#### Scenario: Orphan and missing records stay distinguishable
- **WHEN** one expected control has no record and a different produced record
  has no expected identity
- **THEN** the joined collection exposes `missing_applicability_record` and
  `unknown_applicability_record_identity` as distinct integrity reasons

#### Scenario: Unassessable reason with foreign provenance is rejected
- **WHEN** an `unassessable` record for control A carries a reason whose family,
  control, or policy provenance belongs to a different control
- **THEN** the record is invalid contract-integrity evidence, the assessment is
  unassessable, and output contains only the canonical invalid-record-integrity
  provenance rather than the foreign reason provenance

### Requirement: Family evidence retains its native units
Applicability records SHALL retain family-specific evidence as drill-down
authority. For every evidence dimension that is meaningful to the family and
applicable to the configured control, evidence SHALL identify its native unit
and contain deterministic counts with canonical item/provenance references.
The shared evidence vocabulary MAY include subjects in scope, matched subjects,
unmapped subjects, ambiguous subjects, stale declarations, and external input
or run presence. A record SHALL omit a dimension that has no semantic meaning
for its family or configured control rather than fabricate a zero, null, or
generic not-applicable placeholder.

The model SHALL NOT combine types, topology nodes, dependency edges, files,
projects, assemblies, metrics, controls, or external runs into a generic score
or cross-family percentage. Counts SHALL be derived from the record's canonical
evidence collection and preserve the unit they count.

#### Scenario: Cross-family evidence is not arithmetically merged
- **WHEN** topology has unmapped observed subjects and external diagnostics has
  a missing required SARIF run
- **THEN** the resulting evidence retains the topology-subject and external-run
  units separately and does not add them into one completeness percentage or
  count

#### Scenario: Families omit irrelevant native dimensions
- **WHEN** a topology control has no external-run semantics and an external
  SARIF control has no subject-mapping semantics
- **THEN** the topology record omits external-run dimensions and the SARIF
  record omits subject-mapping dimensions while each retains its applicable
  native counts, references, and provenance

### Requirement: Applicability records have deterministic ordering and provenance
The canonical applicability collection SHALL order records by canonical
effective-control identity. Each record's evidence categories and item
references SHALL use their stable semantic identities and deterministic order.
Canonical equality SHALL not depend on display text, runtime enumeration order,
local timestamps, absolute paths, or random identifiers. Provenance SHALL be
sufficient to drill into the canonical control and its family evidence without
becoming a second control or finding identity.

#### Scenario: Equivalent analysis repeats identically
- **WHEN** the same effective policy and family evidence are evaluated twice
- **THEN** the control records, native evidence ordering, and provenance
  references are canonically equivalent across the two results

### Requirement: Family applicability matrices are explicit
The shared design SHALL define the following v0.8 applicability matrices for
the families that opt in:

| Family | Required evidence | Evaluable condition | Unassessable examples |
| --- | --- | --- | --- |
| Declared topology (#91) | declared topology control, observed subject universe, mapping and declaration evidence | every required observed subject is deterministically mapped or explicitly reviewed out of scope and declarations are current | missing subject universe, unmapped or ambiguous required subject, stale declaration |
| Contract-surface exposure (#92) | selected contract surface, visible signature/metadata facts, source and target classification evidence | the selected surface and required facts resolve completely for the configured control | missing selected surface, unresolved required fact, unexpected empty selector, stale declaration |
| Metrics and budgets (#93) | metric definition, target subject universe, and the measurement facts required by that metric | the metric's native counting universe and contributors are complete enough to trust its value | incomplete or unmapped target scope, ambiguous component, missing required measurement fact, unexpected empty input |
| External static diagnostics (#95) | logical evidence requirement, bounded SARIF artifact/run, and required trust binding | a successful matching run satisfies configured repository, revision, scope, producer, and logical-evidence binding | missing, malformed, failed, stale, wrong-key, wrong-repository, wrong-revision, or wrong-scope required input |

Each family SHALL make its own exact subject and declaration semantics explicit
when it implements a control; it SHALL use this matrix rather than inventing a
parallel result envelope.

#### Scenario: Valid current SARIF with no selected findings
- **WHEN** an external-diagnostics control receives a valid, successful,
  current-context SARIF run with zero selected diagnostics
- **THEN** its record is `evaluable` and distinguishes that state from a missing
  or stale required run

#### Scenario: Optional external evidence is not a failed required run
- **WHEN** an explicitly optional external-evidence control has no supplied
  artifact
- **THEN** its record is `not_applicable` with optional-policy provenance, not
  `unassessable` and not a successful zero-result run

### Requirement: Summaries derive from canonical membership and states
Downstream consumers SHALL first validate produced record cardinality and
anti-join produced identities against the canonical expected
applicability-membership collection. They SHALL then derive applicability
transparency summaries by left joining expected entries to compatible produced
records on canonical effective-control identity. They SHALL derive the required
denominator from expected entries whose membership is `required`, and the
evaluable numerator only from joined required entries with a valid `evaluable`
state. Missing required records and invalid joined records SHALL remain required
denominator members and have an unassessable integrity outcome.

An unknown/orphan produced identity, duplicate record, missing record, or
incompatible record SHALL be separately disclosed as collection-integrity
evidence and SHALL prevent the canonical applicability collection from being
reported as complete. A consumer MAY display the required evaluable ratio beside
that integrity status, but SHALL NOT present the ratio as a complete or clean
summary while integrity evidence is unassessable. Optional and not-applicable
entries and their records or integrity outcomes SHALL remain separately
disclosed without inflating the required denominator.

Consumers SHALL NOT count findings, YAML rules, rule categories, source-set
expansions, display strings, or independently inferred family semantics to
construct membership or the denominator. If the required expected-membership
collection is unavailable, malformed, or cannot be tied to the effective-policy
context, the applicability summary itself SHALL be unassessable and SHALL NOT
fabricate a zero or reduced denominator. The model SHALL permit deterministic
summaries such as `38/38 evaluable` only when all 38 explicit expected required
members join to valid evaluable records and the collection has no unassessable
integrity evidence. It SHALL permit effective policy inventory to be displayed
beside the summary while preserving the fact that inventory/counting is owned
by #685.

#### Scenario: Missing record cannot improve a summary
- **WHEN** an expected collection has 38 required entries, 37 join to valid
  evaluable records, and one required entry has no produced record
- **THEN** the summary is unassessable, reports `37/38 evaluable` with one
  unassessable required control, and does not emit `37/37` or complete

#### Scenario: Orphan record prevents a clean summary
- **WHEN** an expected collection has one required entry A, A joins to a valid
  evaluable record, and produced records also contain unknown identity X
- **THEN** the consumer may display `1/1 evaluable` but marks the collection
  and summary unassessable with `unknown_applicability_record_identity`; it
  does not report a clean or complete `1/1` result

#### Scenario: Inventory and evaluability denominators differ
- **WHEN** the effective-policy inventory contains 42 controls and the expected
  applicability collection contains 38 required entries
- **THEN** the downstream consumer can display `42` effective controls and
  `38/38 evaluable` only after the expected/record join proves every required
  entry evaluable and collection integrity is valid, without recalculating
  either authority or presenting the two counts as interchangeable

### Requirement: Existing governance behavior remains unchanged until opt-in
The shared applicability contract SHALL not by itself add policy fields, alter
existing coverage classification, change strict/audit outcomes, create
normalized findings, change baseline identity, or affect existing policy
behavior. #506 owns fail-closed enforcement and #507 owns normalized
Human/JSON/SARIF/Testing projection; later family implementations opt in using
this contract.

#### Scenario: Existing policy has no v0.8 applicability control
- **WHEN** a policy contains only currently supported, pre-v0.8 contracts
- **THEN** it has identical loading, validation, finding, baseline, and output
behavior after this applicability contract is introduced

### Requirement: Applicability evidence drives authoritative completion
Every v0.8 family that opts into applicability SHALL project its canonical
expected-membership and produced-record integrity result to the shared
governance assessment-completion boundary. A required expected entry that is
missing, duplicate, orphaned, incompatible, unexpectedly empty, stale,
unmapped, ambiguous, or otherwise insufficient SHALL be unassessable rather
than being inferred from a zero finding count. Explicit optional and
not-applicable membership SHALL remain visible without changing the required
denominator.

#### Scenario: Complete records permit trusted conformance
- **WHEN** every required expected applicability entry joins to one compatible
  evaluable record and no configured architecture control fails
- **THEN** the family supplies trusted evaluable evidence that can contribute
  to authoritative assessment `pass`

#### Scenario: Collection-integrity evidence prevents a clean completion
- **WHEN** a required expected entry has no compatible produced record or the
  produced collection contains an orphan identity
- **THEN** the family supplies canonical unassessable completion evidence with
  the collection-integrity reason and does not report a clean empty result

### Requirement: Collection integrity preserves every expected and duplicate provenance
An expected applicability entry's provenance SHALL use that entry's canonical
effective-control identity and family. An applicability collection SHALL have
exactly one compatible produced record for every canonical expected identity,
regardless of whether its membership is `required`, `optional`, or
`not_applicable`. A missing record SHALL produce `missing_applicability_record`
integrity evidence and SHALL prevent the collection from being reported as
complete; an intentionally absent optional input SHALL instead produce an
explicit compatible `not_applicable` record.

An expected entry with mismatched control or family provenance SHALL produce
invalid-expected-integrity evidence with canonical provenance for that expected
identity and SHALL prevent the collection from being reported as complete.

For duplicate expected identities, consumers SHALL select the displayed
representative by a deterministic ordering of membership, family, and complete
provenance, and SHALL retain a duplicate-identity reason for every participating
expected provenance. For duplicate produced identities, consumers SHALL retain
a duplicate-identity reason for every participating produced provenance in
deterministic order. Enumeration order SHALL NOT affect the representative,
required denominator, reason list, or completion state.

#### Scenario: Missing optional record is integrity evidence
- **WHEN** an expected `optional` control has no produced record
- **THEN** the joined projection exposes `missing_applicability_record`, the
  required denominator remains unchanged, and the collection is not complete

#### Scenario: Mismatched expected provenance is invalid authority
- **WHEN** an expected entry for control A in family F carries provenance for a
  different control or family, while a produced record otherwise matches A/F
- **THEN** the join exposes invalid-expected-integrity evidence with A/F
  provenance and completion is `unassessable`, not `pass`

#### Scenario: Duplicate records retain producer provenance
- **WHEN** two produced records for one expected identity have distinct policy
  provenance
- **THEN** collection integrity exposes one deterministic duplicate-record
  reason for each producer provenance

#### Scenario: Reordered duplicate expectations are equivalent
- **WHEN** the same conflicting duplicate expected entries are supplied in
  opposite enumeration orders
- **THEN** the displayed representative, required denominator, reasons, and
  completion state are identical

### Requirement: Declared topology retains native completeness evidence
The declared-topology applicability control SHALL retain deterministic native
facts for the configured subject universe, mapped subjects, reviewed
out-of-scope subjects, unmapped subjects, ambiguous subjects, and enabled stale
declarations. It SHALL use the existing
control identity, applicability membership/state, reason codes, and
provenance; it SHALL NOT introduce a topology-only result envelope or quality
percentage.

#### Scenario: Exhaustive topology is complete
- **WHEN** every observed subject in an exhaustive topology is exactly-one mapped or reviewed out of scope
- **THEN** its applicability record can be evaluable with ordered native mapping evidence

#### Scenario: Mapping gap is unassessable
- **WHEN** an exhaustive topology has an unmapped or ambiguous required subject
- **THEN** its record is unassessable with the existing unmapped-subject or ambiguous-subject reason and native drill-down evidence
