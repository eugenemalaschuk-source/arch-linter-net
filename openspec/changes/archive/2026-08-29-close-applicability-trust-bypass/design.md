## Context

See proposal.md for motivation. The snapshot currently evaluates the canonical
expected-membership and produced-record collections, but accepts a completion
value supplied alongside them when both collections are empty. It also treats a
non-empty reason list on an `unassessable` record as valid without validating
each reason's provenance.

## Goals / Non-Goals

**Goals:**

- Keep canonical collection evaluation as the sole authority for applicability
  completion.
- Prevent foreign reason provenance from reaching reporting models.
- Cover both trust-boundary regressions with focused NUnit tests.

**Non-Goals:**

- Add a new transport-only completion protocol.
- Change ordinary conformance, policy validation, or family-specific evidence
  semantics.

## Decisions

### Remove the execution-result completion transport property

The execution result will carry only expected membership and produced record
collections. The snapshot derives completion only by evaluating those
collections. Removing the public transport member makes the unsafe path
unrepresentable to current and future family executors; it is preferred to an
opt-in marker because a marker would add another contract that could be applied
incorrectly.

### Validate every unassessable reason against record provenance

For an unassessable record, each reason must exactly match the record
provenance's family, control identity, and policy identity. A missing reason or
any mismatch invalidates the complete record and produces the existing canonical
invalid-record-integrity reason using the record provenance. The untrusted
reasons are not copied into the assessment.

### Preserve existing completion compatibility

Empty canonical collections continue to mean that no applicability completion
is emitted. The established ordinary conformance result remains authoritative
for policies whose families have not opted into applicability evidence.

## Risks / Trade-offs

- [Public API removal affects callers that set the transport property] → This
  is an intentional breaking change in the preview API and is recorded in the
  reviewed API snapshots.
- [Strict reason matching can reject incomplete family evidence] → Tests and
  fixtures will always construct unassessable reasons with the exact canonical
  policy provenance.

## Migration Plan

1. Remove uses of the precomputed completion transport member.
2. Update reviewed public API baselines to record the removal.
3. Validate focused and full Core tests, architecture policy, API contract, and
   OpenSpec before publishing the patch.
