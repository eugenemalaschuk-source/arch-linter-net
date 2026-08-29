## Context

See [proposal.md](proposal.md) for the review finding. The existing main spec
correctly prohibited inferring evaluability from zero findings, but made
membership a property of the record that might be missing. Consequently, a
consumer could not reconstruct which effective controls were required without
independently reinterpreting policy/family semantics.

## Goals / Non-Goals

**Goals:**

- Separate expected applicability membership from produced assessment records.
- Make a missing or incompatible record fail closed without shrinking the
  required denominator.
- State the complete membership × state invariants for consistent family
  implementations.

**Non-Goals:**

- Introduce an additional policy inventory, effective-rule count, or health
  algorithm.
- Decide #506 exit/gate behavior for optional evidence or change #507's
  normalized output structure beyond the shared contract it must project.
- Add any executable evaluator, schema, CLI, public API, or existing-policy
  behavior.

## Decisions

### 1. Materialize expectations before evaluation records

The effective-policy/control projection materializes an ordered expected
membership collection for all effective v0.8 applicability-aware controls. It
is the authoritative answer to whether a control is required, optional, or not
applicable for the analyzed effective-policy context. A family evaluator then
produces record state/evidence keyed to those expectation identities; it does
not decide membership again.

```text
effective control projection
          │
          ▼
expected applicability controls ─────┐
  A required                          │ left join on canonical control identity
  B required                          ▼
  C optional                  produced applicability records
                               A evaluable
                               C not_applicable
                                        │
                                        ▼
                         A evaluable; B unassessable (missing record);
                         C optional/not_applicable
```

This gives #507 a total, stable input for denominator calculation and allows it
to synthesize the missing-record state. #685 can consume the same control
identities, but does not own or rebuild this membership collection.

Alternative considered: retain membership only in records and ask consumers to
infer it when a record is absent. Rejected because that duplicates family
semantics and makes a missing record able to improve a summary.

### 2. Make membership authoritative and state conditional

Records reference the expectation; they do not contain an independent,
potentially divergent membership field. The spec's exhaustive table is the
compatibility contract: required controls evaluate or are unassessable;
optional controls are evaluable when supplied and complete, not applicable when
intentionally absent, and unassessable when supplied evidence is bad;
not-applicable controls only have not-applicable state.

The optional-invalid case remains `unassessable`, not an automatic policy error
classification. #506 decides the fail-closed/gate consequences and #507
projects the typed result; neither may relabel it as optional absence.

Alternative considered: collapse optional supplied-invalid evidence into
`not_applicable`. Rejected because it hides a real, supplied but untrustworthy
input and violates the non-inferential rule.

### 3. Treat missing and incompatible records as integrity evidence

The expected/record join detects duplicate, absent, unknown-identity, and
invalid-state records. These become stable unassessable integrity evidence;
required entries remain in the denominator. If the expected membership
collection itself is missing or cannot bind to the effective policy context,
the entire applicability summary is unassessable rather than a fabricated
empty/green result.

## Risks / Trade-offs

- [A second inventory is introduced] → Expected membership contains only the
  applicability classification for already-effective identities; it neither
  discovers nor counts effective controls.
- [Optional absence is mistaken for invalid evidence] → The exhaustive table
  reserves `not_applicable` for explicit optional absence and reserves
  `unassessable` for supplied insufficient evidence.
- [Consumers implement mismatched joins] → Specify one left-join direction,
  identity, missing-record reason, and denominator rule.

## Migration Plan

This is a design-only correction before any family implementation opts in. It
changes no current policy or runtime artifact. Later #506/#507 work materializes
the two collections together for the same effective-policy context.
