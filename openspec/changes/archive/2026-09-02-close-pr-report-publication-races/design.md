## Context

The architecture PR report uses an untrusted, read-only CI producer and a trusted
`workflow_run` publisher. The existing trust boundary is retained, but production CI exposed four
correctness gaps: Health always requires a baseline, publication has a stale-head race, a partial
rerun can erase good same-head evidence, and lenient UTF-8 decoding can change validated bytes.

## Goals / Non-Goals

**Goals:**

- Keep the CLI Health baseline contract while allowing repositories that have not adopted a
  reviewed baseline to produce a canonical report.
- Make a stale report fail closed before publication and replace a detected post-write stale report
  with fixed unavailable metadata.
- Preserve a current, already-verified report when a partial failed-job rerun did not rerun the
  producer.
- Publish only a string decoded once from strictly validated UTF-8 report bytes.

**Non-Goals:**

- Change Health's public CLI contract or persist an empty baseline in the repository.
- Turn the publisher into an overall-CI status aggregator, execute artifact content, or add a
  privileged checkout.
- Eliminate GitHub's inherently non-atomic push/comment API window; detected races must instead
  be made conservative.
- Optimize the producer's independent architecture traversals.

## Decisions

### Supply an ephemeral canonical empty baseline to Health

When `architecture/baseline.arch.yml` is absent in the producer current worktree, the producer
writes `version: 3`, an empty `baseline`, and empty `metric_baselines` to a uniquely named file in
`RUNNER_TEMP`, and supplies that path only to Health. This means no accepted architecture debt and
preserves the CLI's required-baseline contract. Change snapshots remain independently conditional
per base/current worktree. Changing Health to accept no baseline would alter its public semantics;
requiring a checked-in baseline would contradict the existing optional-baseline producer contract.

### Treat only a real Health schema as renderable evidence

After Health exits, the producer parses its JSON and requires `schema_id` equal to
`architecture-health/v1`. Valid failing and unassessable Health documents continue to render; a
command-error envelope or malformed document fails producer integrity before `report pr` runs.

### Guard comment writes with before-and-after head reads

The publisher reads the PR head immediately before `createComment` or `updateComment`; mismatch
rejects without writing. After a write it reads the head again. If a newer head is detected, it
updates the comment it just wrote with fixed unavailable metadata bound to that newer head. This
does not assume an unavailable report is a verdict and prevents a detected old PASS from appearing
current. A completely atomic compare-and-write is not exposed by the GitHub comment API.

### Distinguish a missing producer in a partial rerun from a failed producer

The job resolver reports `producer_missing` when the latest attempt has no producer job. The
comment path leaves exactly one existing current-head unified comment untouched in that case.
Missing evidence without that verified current-head comment, ambiguity, failure, cancellation, and
transport errors remain fail closed and can publish only bounded unavailable metadata.

### Decode the report once with fatal UTF-8

Artifact validation computes the byte hash and count first, then uses `TextDecoder` with
`fatal: true`. It returns the decoded string as the only publication source. A decoding error is a
transport failure, so no replacement-character normalization can make an artifact different from
the byte sequence that was validated.

## Risks / Trade-offs

- [A producer temporary baseline diverges from a future baseline format] → Use the currently
  canonical version-3 empty structure and add workflow-contract assertions.
- [A push occurs after the final post-write read] → GitHub offers no conditional comment write;
  the publisher still binds its marker to the report head and repairs every race it observes.
- [A partial rerun preserves an old malformed comment] → Preservation is limited to exactly one
  bot-authored unified marker explicitly bound to the current head; all other cases stay fail
  closed.
- [Arbitrary valid Markdown from a fork is bot-authored] → The accepted UX model is inert
  presentation only; no source/artifact execution, checkout, or extra producer permission is
  introduced.

## Migration Plan

1. Update producer and publisher behavior together with executable fixture tests.
2. Run workflow and publisher test suites locally, then push the fix and rely on a new PR CI run
   to prove the Health path.
3. If a regression occurs, revert the workflow commit; existing reports remain marker-bound and no
   repository baseline migration is required.

## Open Questions

None.
