## MODIFIED Requirements

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
entry; a record SHALL NOT define a competing membership value. The expected
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
  and B in the required denominator, and represents B as `unassessable` with a
  `missing_applicability_record` reason rather than inferring membership from
  family semantics or omitting B

#### Scenario: Optional control is visible without inflating the denominator
- **WHEN** explicit policy semantics make configured control C optional
- **THEN** C has an explicit expected `optional` membership entry, remains
  separately disclosed whether its record is evaluable, not applicable, or
  unassessable, and does not inflate the required denominator

### Requirement: Applicability state is typed and non-inferential
Each produced applicability record SHALL expose exactly one typed state:
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

No other membership/state combination is valid. In particular,
`required`/`not_applicable`, `not_applicable`/`evaluable`, and
`not_applicable`/`unassessable` SHALL NOT be interpreted as valid outcomes. A
missing produced record for any expected entry, or a record whose identity or
state is incompatible with its expected entry, SHALL be represented as
unassessable contract-integrity evidence. A missing `required` record SHALL
always remain in the denominator.

Zero findings, a zero measured value, an empty result collection, a configured
control count, or a missing record SHALL NOT be inferred to mean `evaluable`.

#### Scenario: Empty successful result remains evaluable only with proof
- **WHEN** a required control produces zero findings or a neutral measurement
- **THEN** it is `evaluable` only if its record proves the required input and
  family evidence were complete; otherwise it is `unassessable`

#### Scenario: Missing record cannot improve a summary
- **WHEN** an expected entry has required membership but its produced
  applicability record is absent
- **THEN** consumers preserve the expected entry in the required denominator
  and represent it as `unassessable` with a `missing_applicability_record`
  reason rather than inferring a complete result

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
An `unassessable` record or synthesized missing-record result SHALL contain one
or more deterministic reason classes and canonical provenance sufficient to
identify the insufficient evidence. Supported reason classes SHALL distinguish,
where meaningful to the family, missing or unavailable required input,
unexpected empty input, unmapped subject, ambiguous subject, stale declaration,
malformed or failed external input, wrong external evidence identity,
repository, revision, or scope, `missing_applicability_record`, and invalid
applicability-record identity or state.

Families MAY define additional bounded reason classes but SHALL NOT use display
text as the machine-readable reason.

#### Scenario: Stale and missing evidence stay distinguishable
- **WHEN** one control has a declared selector with no current match and another
  has no produced applicability record
- **THEN** their results expose distinct stable reason classes and provenance
  rather than a shared zero-result status

### Requirement: Summaries derive from canonical membership and states
Downstream consumers SHALL derive applicability transparency summaries by left
joining the canonical expected applicability-membership collection with
produced records on canonical effective-control identity. They SHALL derive the
required denominator from expected entries whose membership is `required`, and
the evaluable numerator only from joined required entries with valid
`evaluable` state. Missing required records and invalid joined records SHALL
remain required denominator members and be unassessable.

Consumers SHALL NOT count findings, YAML rules, rule categories, source-set
expansions, display strings, or independently inferred family semantics to
construct membership or the denominator. If the required expected-membership
collection is unavailable, malformed, or cannot be tied to the effective-policy
context, the applicability summary itself SHALL be unassessable and SHALL NOT
fabricate a zero or reduced denominator. Optional and not-applicable entries
and their records SHALL remain separately disclosed.

The model SHALL permit deterministic summaries such as `38/38 evaluable` only
when all 38 explicit expected required members join to valid evaluable records.
It SHALL permit effective policy inventory to be displayed beside the summary
while preserving the fact that inventory/counting is owned by #685.

#### Scenario: Missing record cannot improve a summary
- **WHEN** an expected collection has 38 required entries, 37 join to valid
  evaluable records, and one required entry has no produced record
- **THEN** the summary is unassessable, reports `37/38 evaluable` with one
  unassessable required control, and does not emit `37/37` or complete

#### Scenario: Inventory and evaluability denominators differ
- **WHEN** the effective-policy inventory contains 42 controls and the expected
  applicability collection contains 38 required entries
- **THEN** the downstream consumer can display `42` effective controls and
  `38/38 evaluable` only after the expected/record join proves every required
  entry evaluable, without recalculating either authority or presenting the two
  counts as interchangeable
