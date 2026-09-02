## Why

Declared topology can now evaluate a policy deterministically, but reviewers still need a
deliberate workflow to inspect observed topology, compare it with the declared model, and
verify it without treating a successful analysis as permission to rewrite policy. This closes
the reviewable capture/diff/verify lifecycle promised by issue #511 and the parent topology
governance story.

## What Changes

- Add a `topology` CLI workflow with deterministic machine-readable capture output for the
  current first-party topology observations and review-oriented policy draft candidates.
- Add declared-versus-observed topology comparison output that keeps structural mapping,
  relational edge, unmapped/ambiguous applicability, and stale-declaration drift distinct.
- Add an explicit verification operation that invokes the same declared-topology evaluator and
  preserves normal strict/audit result semantics rather than introducing a second evaluator or
  result envelope.
- Document the review-first workflow and add realistic .NET server/library and Unity fixtures
  proving capture, diff, and verification behavior.

## Capabilities

### New Capabilities

- `topology-review-workflow`: Review-only capture, diff, and verification commands over the
  native declared-topology model and canonical evaluator.

### Modified Capabilities

- None.

## Impact

- CLI command registration, command handlers, and stable JSON/human rendering.
- Core topology observation/evaluation projection APIs needed by the workflow, while retaining
  existing validation and applicability boundaries.
- NUnit coverage and documented topology-policy guidance.
