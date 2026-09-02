## Context

`report pr` already renders the canonical architecture-only Markdown from an
`architecture-health/v1` document and compatible architecture-change report. The current `ci.yml`
instead produces raw coverage artifacts and writes a separate Architecture Coverage comment inside
the pull-request workflow. That workflow executes pull-request code, so it cannot safely own a
write-capable token.

The report producer may execute untrusted pull-request code with read-only permissions. The
publisher must run from trusted default-branch workflow definition and treat every downloaded
artifact byte as inert, untrusted data.

## Goals / Non-Goals

**Goals:**

- Produce the complete PR report with the existing Core/CLI commands from a read-only producer.
- Bind one fixed report artifact to repository, PR, current head SHA, CI run ID, and run attempt.
- Verify fixed artifact metadata, shape, byte limits, manifest schema/kind/marker, SHA-256, and
  report byte count before a single least-privilege job writes a sticky comment.
- Replace the legacy Architecture Coverage sticky comment in place where present, while retaining
  coverage commands and artifacts.
- Fail closed without publishing stale architecture evidence; use only bounded, fixed
  transport/integration messages when a report cannot be trusted or produced.

**Non-Goals:**

- Altering Core/CLI report semantics, Architecture Health precedence, external-diagnostic
  normalization, or coverage behavior.
- Executing PR content, checking out a PR ref, evaluating report/manifest text as code, or
  querying unrelated CI or external-service status in the privileged publisher.
- Guaranteeing a comment for untrusted forks by weakening the trust boundary.

## Decisions

### Split producer and publisher at the GitHub Actions trust boundary

The existing `pull_request` CI workflow will generate Health and change artifacts, run
`arch-linter-net report pr`, and upload one fixed report artifact using only `contents: read`.
A new `workflow_run` workflow, triggered only when `CI` completes, is the sole job with
`pull-requests: write`.

`workflow_run` is chosen over a write-capable `pull_request` job because it executes the workflow
definition from the default branch and therefore separates publication from untrusted PR checkout
and execution. The publisher performs no checkout at all. A `pull_request_target` publisher was
rejected because it would make it too easy to execute PR-controlled code with write credentials.

### Use a fixed, manifest-bound artifact protocol

The producer uploads exactly one named artifact containing only:

- `architecture-pr-report.md`, the exact Markdown written by the existing CLI; and
- `architecture-pr-report.manifest.json`, a fixed-schema manifest containing the repository, PR
  number, head SHA, producer run ID and attempt, report kind/schema/marker version, file name,
  byte count, and SHA-256.

The publisher first asks GitHub for the completed run's artifacts and rejects missing, duplicate,
expired, or oversized artifacts before downloading by ID. It then accepts only the two fixed,
regular files at the expected paths, checks tight manifest/report limits, parses JSON without
executing it, compares every identity field with GitHub event and current-PR data, and recomputes
the report hash. It does not interpret report content as architecture state. The action-provided
artifact digest is an additional transport check; the manifest hash binds the exact report bytes.

The report renderer remains responsible for semantic detail bounds. The publisher applies a second
fixed transport ceiling and rejects, rather than truncates, an oversized report.

### Make stale updates non-authoritative

The publisher reads the current PR head immediately before publication. It refuses a producer run
whose event head differs from that head. Hidden context metadata on the one repository-owned
comment identifies the published head/run, so a late stale workflow cannot overwrite a newer
current-head report. When no current report exists, a fixed unavailable message may replace old
evidence; it states only a transport reason and never fabricates a health result.

### Own one comment and migrate the legacy comment marker

The publisher adds a stable unified-report marker and updates the unique bot-authored matching
comment. When the old bot-authored Architecture Coverage marker is the only existing report,
the publisher updates that same comment to the new report rather than creating a competitor.
Ambiguous duplicate bot comments fail closed rather than selecting an arbitrary one.

### Verify workflow contracts as source-level security invariants

Focused Python workflow-contract tests will assert the producer's read-only permissions and CLI
ownership, the publisher's no-checkout / one-writer structure, fixed artifact protocol and
validation gates, stale-head handling, and removal of the legacy comment writer. This is the
appropriate repository-level regression evidence for declarative workflow behavior; GitHub itself
executes the event and token topology in PR CI.

## Risks / Trade-offs

- [A producer is cancelled, fails before upload, or emits malformed evidence] → The publisher
  writes only a bounded unavailable/integration state or preserves a newer verified report; it
  never reuses a previous PASS report as current evidence.
- [A force push races a completed producer] → Current-head comparison and hidden context metadata
  prevent the older run from becoming authoritative.
- [A fork uploads hostile bytes] → The privileged workflow never checks out or executes those
  bytes, accepts a single bounded fixed shape, and uses explicit parsing/hash comparisons only.
- [Large comments exceed GitHub transport limits] → Core bounds semantic detail and the publisher
  rejects bytes beyond its fixed ceiling without changing report meaning.
- [Workflow-run permissions differ in a GitHub deployment] → Publication fails closed and the
  documented limitation directs maintainers to preserve the separation rather than broaden PR-job
  credentials.

## Migration Plan

1. Merge the read-only artifact producer and publisher together so no period exposes a writer in
   the PR-code job without its replacement.
2. The first successful unified publisher updates an existing legacy bot comment in place or
   creates one unified comment.
3. Roll back by reverting the change; raw coverage artifacts and the standalone coverage command
   remain intact throughout.

## Open Questions

None. GitHub's pinned `download-artifact` action supports exact cross-run artifact IDs, run IDs,
and a GitHub token, so the publisher can validate the artifact inventory before it downloads.
