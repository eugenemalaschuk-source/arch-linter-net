## Context

The first evaluated-manifest hardening change deliberately fails closed when static evidence is
incomplete. The remaining review findings are cross-cutting: callers do not pass Platform/RID to
the preflight subsystem, lexical containment can hash a file reached through a reparse-point
ancestor, budget rejection does not stop enumeration, and receipt verification treats matching
ineligibility as a mismatch.

## Goals / Non-Goals

**Goals:**

- Preserve Platform/RID from public validation requests to build output selection and receipts.
- Reject any path that crosses a reparse point before its content is read, including an ancestor
  directory.
- Bound traversal as well as hashes and retained inputs.
- Make manifest/receipt agreement compare the complete cache outcome rather than requiring an
  eligible outcome.

**Non-Goals:**

- Make static collection cache-eligible; it remains deliberately fail-closed.
- Add recursive evaluated-MSBuild execution or external input declarations.
- Alter the unrelated Windows duration and process-cleanup test infrastructure.

## Decisions

- Add optional Platform and RuntimeIdentifier to the existing request records and pass them using
  named arguments through validation, snapshot, preflight, output discovery, and receipt writes.
  This keeps source compatibility for callers while making context identity explicit.
- Walk each candidate's ancestor chain from the candidate to the repository root and reject a
  reparse point before `File.Exists`, metadata reads, or hashing. This rejects links rather than
  resolving targets, avoiding target-dependent identity and cross-platform link semantics.
- Represent collector exhaustion as shared state. `AddFile` checks the state before filesystem
  probes; the source enumeration exits at the first exhausted budget, so it cannot continue to
  traverse a hostile tree.
- Compare receipt manifest digest, eligibility, and normalized ineligibility reasons to the
  freshly collected outcome. An agreed `cache-ineligible` outcome is valid fail-closed evidence;
  only a disagreement adds `receipt-manifest-mismatch`.

## Risks / Trade-offs

- [Legitimate repository symlink is rejected] → This is intentional until a typed, canonical
  external-input model exists; diagnostics remain cache-ineligible rather than authorizing reuse.
- [Optional fields expand public API] → Preserve defaults and update API approval tests/schema.
- [Platform/RID output layout is SDK-specific] → Use the existing output search constraints and
  add only requested path-segment checks without assuming a single RID layout.

## Migration Plan

1. Add the request and propagation fields with null defaults.
2. Harden collection and receipt comparison with regression tests.
3. Update public API approvals and schema compatibility material.
4. Run the complete validation suite; no persisted receipt migration is required because old
   receipts already remain cache-ineligible.
