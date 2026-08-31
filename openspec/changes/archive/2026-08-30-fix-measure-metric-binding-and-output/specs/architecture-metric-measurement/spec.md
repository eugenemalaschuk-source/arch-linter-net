## MODIFIED Requirements

### Requirement: The measure command emits deterministic Human and JSON reports
The CLI SHALL provide `measure` as a read-only command accepting a policy path,
optional metric-ID selection, and `human` or `json` output. It SHALL report
all declared metrics when no selection is supplied and reject unknown selected
IDs as invalid arguments. Human output SHALL show each metric's identity,
kind, native subject/effective scope, state, exact value when evaluable, and
bounded contributor evidence. JSON output SHALL use a documented schema
version and contain the same data with stable ordinal ordering. For an
evaluable measurement, JSON SHALL include the total contributor count and an
explicit truncation indicator when the requested contributor bound omits
entries. For an unassessable measurement, JSON SHALL use `null` for `value`,
`contributor_count`, `contributors`, and `contributors_truncated`; it SHALL
not serialize an unknown contributor universe as an empty list or zero count.
The command SHALL permit an explicit all-contributors request.

#### Scenario: JSON output is repeatable and schema-versioned
- **WHEN** the same policy, selected metric IDs, and analysis inputs are
  measured twice
- **THEN** the JSON documents have the same schema version, ordering, values,
  scope evidence, and contributors

#### Scenario: A bounded report preserves evidence about omitted contributors
- **WHEN** an evaluable metric has more contributors than the selected display
  bound
- **THEN** the report emits the bounded ordinal prefix, the full contributor
  count, and a true truncation marker

#### Scenario: An unassessable report does not imply an empty contributor universe
- **WHEN** a metric is unassessable because a required fact or topology endpoint
  is incomplete
- **THEN** its JSON `value`, `contributor_count`, `contributors`, and
  `contributors_truncated` fields are `null` rather than a numeric zero, empty
  list, or false truncation claim
