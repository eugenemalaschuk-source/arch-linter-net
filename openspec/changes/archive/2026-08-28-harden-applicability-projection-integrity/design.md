## Context

See [proposal.md](proposal.md) for the review findings. The current contract
already makes expected membership independent from produced records, but its
state table accidentally governs both valid records and invalid collection
joins. A `not_applicable` expected control with no record is therefore both
permitted by cardinality and forbidden by the table after the join synthesizes
`unassessable`.

## Goals / Non-Goals

**Goals:**

- Keep membership/state compatibility strict for valid produced records.
- Make missing, duplicate, orphan, and incompatible records visible as typed
  integrity evidence without assigning them a false valid record state.
- Preserve the native units and sparse dimensions of each family’s evidence.

**Non-Goals:**

- Add an evaluator, policy schema, normalized output model, exit behavior, or
  second effective-policy inventory.
- Decide #506 enforcement or gating consequences beyond preserving a
  fail-closed, deterministic input for it.

## Decisions

### 1. Model record state and join integrity independently

The membership × state matrix applies only after exactly one produced record
has been matched to the expected identity and that record is compatible with
the expected membership. A joined per-control projection exposes the valid
produced-record state separately from an integrity outcome. Missing, duplicate,
or incompatible records have an `unassessable` integrity outcome and no valid
produced-record state.

For example, an expected `not_applicable` control with no record is represented
as `produced state: absent` and `integrity: unassessable` with
`missing_applicability_record`; it is not represented as the forbidden produced
record combination `not_applicable`/`unassessable`. It remains outside the
required denominator but cannot be silently treated as a valid outcome.

Alternative considered: permit `not_applicable`/`unassessable` in the record
state table. Rejected because it makes malformed evidence indistinguishable
from a valid explicit non-applicability decision.

### 2. Validate the entire record collection before the left join

Consumers first validate record cardinality and perform an anti-join from
produced identities to expected identities. Only then do they left join each
expected identity to its compatible record. Unknown/orphan records make the
collection integrity unassessable even though a left join alone would omit
them. The expected collection remains the only membership and denominator
authority.

Alternative considered: rely on the left join to find all malformed records.
Rejected because an expected-to-produced left join cannot retain unmatched
produced identities.

### 3. Use sparse native evidence dimensions

Evidence categories are typed by each family’s native unit. A record materializes
only the dimensions meaningful to both that family and the configured control;
the shared vocabulary is descriptive, not a mandatory cross-family envelope.
This permits topology to omit external-run dimensions and external SARIF to
omit subject-mapping dimensions without fabricating zero or "not applicable"
placeholders.

Alternative considered: require every category in every record. Rejected
because it creates meaningless dimensions and a de facto generic coverage
model, contrary to #505's design boundary.

## Risks / Trade-offs

- [Consumers flatten integrity into a record state] → The normative spec calls
  out the two axes and provides a missing `not_applicable` scenario.
- [Orphan evidence is discarded before validation] → The anti-join is required
  before deriving the summary.
- [Sparse evidence hides required facts] → A family must still materialize each
  dimension meaningful and applicable to its configured control, with native
  counts and provenance.

## Migration Plan

This is a pre-implementation design correction. Later #506 and #507 consume
the clarified contract when they materialize evaluator and projection behavior;
existing policies and runtime behavior do not change.
