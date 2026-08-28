## Context

See [proposal.md](proposal.md) for the motivation. Existing coverage already
models in-scope classification using its own `covered`, `excluded`,
`uncovered`, `unknown`, `stale`, and `empty-input` vocabulary. Exact normalized
finding identity is also established elsewhere. The v0.8 family backlog needs a
cross-family contract for whether effective governance controls could evaluate
their intended surfaces, without replacing either authority.

## Goals / Non-Goals

**Goals:**

- Establish a common, canonical control-level record that later family
  evaluators can create and #506/#507 can consume.
- Keep applicability membership, assessability state, reason classes, units,
  and provenance deterministic and sufficient for summary projection.
- State concrete adoption matrices for #91, #92, #93, and #95 while reserving
  family-specific subject semantics for the owning family issues.

**Non-Goals:**

- Implement a v0.8 evaluator, policy schema, output artifact, finding type,
  baseline key, exit code, or public API.
- Count effective policy controls (owned by #685), reimplement coverage (#57),
  or define the normalized-output/fail-closed seams (#507/#506).
- Define a generic architectural health score or a cross-family percentage.

## Decisions

### 1. Model membership separately from state

Each eventual `ControlApplicabilityRecord` has an opaque canonical effective
control identity, a family discriminator, applicability membership, state,
reason classes, and native evidence. Membership is `required`, `optional`, or
`not_applicable`; state is `evaluable`, `not_applicable`, or `unassessable`.
This makes the required denominator explicit, prevents silently dropping an
optional control, and prevents a missing record from being read as success.

The state/membership combination is constrained: required controls are either
evaluable or unassessable; optional controls may be evaluable when evidence was
supplied or not applicable when policy declares it unnecessary; no control may
be `not_applicable` merely because required input is absent.

Alternative considered: use one status enum. Rejected because it cannot state
both whether a control belongs in the summary denominator and whether supplied
evidence was assessable without deriving one fact implicitly.

### 2. Reuse canonical effective-control identity

The record links to the effective-policy control identity produced by the
policy/inventory authority. Display names, YAML locations, source-set instances,
finding identities, and rendered messages are provenance only and cannot become
the link key. A source-set-expanded rule remains one control record.

Alternative considered: define a new applicability identity. Rejected because
it would make #505 a competing policy-inventory authority and create drift with
#685, policy weakening, and downstream summaries.

### 3. Preserve typed native evidence instead of a generic coverage total

The shared envelope can carry structured evidence dimensions, each with a
native unit, deterministic count, stable item references, and provenance.
Topology retains declared nodes and observed subjects; exposure retains selected
surface facts; metrics retain target/counting-universe facts; external evidence
retains logical keys and run/trust facts. The model permits a family to omit an
inapplicable dimension but forbids relabelling heterogeneous values as one
number.

Alternative considered: reuse `ArchitectureCoverageSummary` wholesale.
Rejected because coverage's classification statuses and units are authoritative
for existing coverage contracts, while a governance control may depend on
external runs or signature facts that are not coverage units.

### 4. Use stable reason classes plus provenance

`unassessable` carries machine-readable classes such as missing input,
unexpected empty input, unmapped subject, ambiguous subject, stale declaration,
malformed/failed external input, and incorrect external trust binding. The
family controls its exact bounded extensions. Human detail is derived from
canonical evidence/provenance rather than becoming an identity or reason key.

Alternative considered: one `incomplete` reason. Rejected because it cannot
support the actionable and fail-closed distinctions required by #504/#506.

### 5. Split responsibility across #505, #506, and #507

#505 owns this terminology, record shape, matrices, and invariants. #506
consumes it at the trusted input boundary to choose fail-closed behavior. #507
projects records through normalized findings and all output hosts, ensuring a
missing required record cannot shrink the denominator. Family issues create
their own evidence using the shared vocabulary. Existing coverage remains an
input/provenance authority where relevant.

## Risks / Trade-offs

- [Premature generic payload] → Limit the shared contract to typed membership,
  state, reason, unit, and provenance; leave exact selectors and evidence data
  shapes to each family.
- [Control/inventory identity divergence] → Require reuse of canonical effective
  control identity and prohibit #505 from recounting controls.
- [False green from zero results] → Require a record plus complete native
  evidence before `evaluable`; #506/#507 enforce this in later work.
- [Metric misuse] → Explicitly prohibit a cross-family score or health
  percentage and retain units at every aggregation boundary.

## Migration Plan

This issue is design-only and introduces no runtime artifact or user-visible
behavior. Later v0.8 family implementations opt in individually. No existing
policy requires migration, and removing the artifacts has no runtime rollback
effect.
