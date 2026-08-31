## Context

See proposal.md. The topology evaluator stores canonical native subject
identity, but the external-fact projection currently falls back to namespace
text and the assembly observation path collapses same-name owners. The report
formatter also treats a withheld contributor set as empty evidence.

## Goals / Non-Goals

**Goals:**

- Preserve canonical owner identity for every metric fact projection.
- Fail closed when a native assembly endpoint has anything other than one
  topology subject candidate.
- Make the JSON distinction between trusted empty and unavailable evidence
  machine-readable.
- Rebuild the approved Core API surface from the project authority.

**Non-Goals:**

- Changing metric kinds, policy syntax, selection flags, report schema version,
  or adding a new output format.
- Broadly redesigning the topology graph or external-dependency scanner.

## Decisions

### 1. Namespace facts use an exact canonical subject identity

External dependency facts resolve their namespace topology source through the
same `subjectKind|project|assembly|subject` identity used by the topology
projection. The namespace-only fallback is removed. It is not safe because
namespace text is a display/category property rather than an owner identity.

### 2. Assembly observation keeps candidate cardinality

The assembly graph will retain a collection of observed subjects per simple
assembly name. A relation is emitted only when each simple-name endpoint binds
to exactly one canonical assembly subject. A relation with a non-unique
endpoint is retained as incomplete endpoint evidence so a selected metric gets
the existing `ambiguous_subject` applicability reason rather than a value.
Choosing the current ordinal `First()` was rejected because it is deterministic
but semantically arbitrary.

### 3. Unknown contributor evidence is null

The formatter will emit `null` for every contributor-evidence field of an
unassessable measurement. `null` preserves the stable keys while proving that
the measurement did not establish a zero-cardinality set. The separate shared
applicability projection continues to explain the reason.

### 4. Regenerate, do not hand edit, the public API snapshot

Run the explicit project API-update target after implementation and commit its
complete generated output. This prevents platform CI from comparing against a
partial hand-maintained surface.

## Risks / Trade-offs

- [An incomplete assembly relation is dropped before metric evaluation] → carry
  deterministic endpoint ambiguity evidence in the topology projection and
  assert it in focused evaluator tests.
- [Nullability surprises downstream JSON clients] → document the existing
  unassessable contract and cover exact JSON tokens in formatter tests.
- [API generation introduces unrelated drift] → inspect the regenerated diff,
  run `make public-api-check`, and retain only reviewed Core surface changes.

## Migration Plan

This is a correctness fix on a draft feature branch. Existing successful
measurements remain unchanged. Consumers of unassessable JSON must treat null
as unavailable evidence; they must never have relied on its previous false
zero. Reverting the commit restores the prior draft behavior.
