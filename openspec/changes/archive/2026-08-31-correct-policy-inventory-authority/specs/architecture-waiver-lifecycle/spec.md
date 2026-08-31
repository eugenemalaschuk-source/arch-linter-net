## MODIFIED Requirements

### Requirement: Waiver lifecycle is deterministic and visible
The system SHALL return one lifecycle record for every configured authored
manual waiver using the states `active`, `stale`, `expired`,
`metadata_incomplete`, and `invalid`. Evaluation SHALL aggregate all selected
expanded-contract aliases of one authored waiver before calculating whether it
matches. A requested authored source-set contract ID SHALL select every
effective alias whose expansion origin has that authored contract ID, using the
same case-insensitive identity semantics as rule execution. Evaluation SHALL
use a date-only `yyyy-MM-dd` evaluation date and include that date in canonical
evidence. State precedence SHALL be invalid, expired, stale,
metadata-incomplete, then active, so an expired waiver remains expired even
when its governed finding is gone. Malformed manual waiver metadata or identity
SHALL produce canonical `invalid` evidence and fail closed rather than
disappearing into an unstructured failure.

#### Scenario: Expired matched waiver is blocking
- **WHEN** a complete strict-profile waiver still matches its governed finding
  and its expiry is before the supplied evaluation date
- **THEN** its state is `expired` and the strict validation outcome fails

#### Scenario: Expired waiver whose finding is gone remains expired
- **WHEN** a complete waiver expires before the supplied evaluation date and no
  longer matches a governed finding
- **THEN** its state is `expired`, with stale-match evidence retained for human
  explanation, rather than disappearing or becoming active

#### Scenario: Invalid waiver remains canonical evidence
- **WHEN** a manual waiver has malformed required structured metadata or target
  identity
- **THEN** validation fails closed and exposes one `invalid` lifecycle record
  with its available authored declaration and policy provenance

#### Scenario: Explicit dates agree across environments
- **WHEN** local and CI validation use the same policy and explicit evaluation
  date
- **THEN** they produce the same lifecycle states and canonical records

#### Scenario: Authored source-set selection retains blocking waiver debt
- **WHEN** validation selects an authored source-set contract ID whose expanded
  alias has an expired, stale, or invalid waiver lifecycle record
- **THEN** that record remains selected, projected into waiver debt, and retains
  its normal blocking behavior
