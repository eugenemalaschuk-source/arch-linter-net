## Context

See [proposal.md](proposal.md). Measurement creates its analysis snapshot before
the evaluator filters the requested metric IDs. Project ownership setup therefore
currently derives its exact-artifact requirement from every definition in the
policy.

## Goals / Non-Goals

**Goals:**

- Validate metric selection and narrow the measurement snapshot definitions
  before project discovery and artifact-resolution decisions.
- Keep non-measurement snapshot behavior and complete-document policy
  validation unchanged.
- Make external dependency grouping proportional to facts plus classifications,
  without changing contributor or applicability semantics.

**Non-Goals:**

- Changing the behavior of an invalid unselected metric definition during policy
  validation.
- Changing topology ownership, contributor identity, or metric output schemas.

## Decisions

### Narrow only the measurement snapshot after policy composition

The snapshot request will carry the selected metric IDs internally. After the
complete policy document is loaded, composed, and validated, snapshot
construction will reuse the evaluator's deterministic selection and replace the
measurement snapshot's metric definitions with that selected subset. All later
setup consumers consequently see only metrics that can be reported.

This preserves complete-document validation while avoiding a public setup API
parameter and prevents selection validation from differing between setup and
evaluation. Passing a filter through every runner setup abstraction was rejected
because it expands a stable general-purpose snapshot seam for a measure-only
concern.

### Index classifications by canonical observed-subject identity

External dependency grouping will construct one ordinal identity lookup from the
topology projection. Source types will still derive the same canonical identity
and then use that lookup for incomplete-source and fact processing. A grouped
lookup preserves behavior if a projection ever contains repeated identities.

## Risks / Trade-offs

- [Selection is applied to a mutable policy document held by a snapshot] → The
  document is freshly composed for that one snapshot, and the operation is
  marked measurement-only; generic validation snapshots keep the full document.
- [Unknown selection fails earlier] → The same shared selector preserves the
  existing invalid-argument message and prevents unnecessary setup work.

## Migration Plan

The change is internal and read-only. Existing callers with no metric selection
retain all definitions; a regression test covers selecting a non-project metric
beside an unavailable unselected project metric.
