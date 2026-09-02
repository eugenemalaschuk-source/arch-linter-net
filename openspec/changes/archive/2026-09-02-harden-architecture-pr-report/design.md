## Context

See [proposal.md](proposal.md) for the motivation.  The existing PR-report
reader independently parses Health and change JSON, then projects all Health
receipts.  Its persisted artifacts do not share a run identity, the
availability map is only loosely interpreted, and the CLI renderer treats
untrusted values as line-oriented text.  The current contracts also place
report composition in `Core.Model` and split key production types across
partial declarations, which violates the repository's self-policy.

## Goals / Non-Goals

**Goals:**

- Make an artifact pair mechanically correlateable and reject every ambiguous
  or incompatible pair before projection.
- Make availability a closed schema with bidirectional payload conformance.
- Preserve canonical blocker meaning without allowing an aggregate gate to
  promote unrelated findings.
- Keep all untrusted Markdown data inert and clean output compact.
- Restore the existing architecture policy without exclusions and reduce
  resolved-finding comparison to linear time.

**Non-Goals:**

- Re-evaluating policy, SARIF, topology, waiver lifecycle, or Health inside
  the PR-report path.
- Changing the Health gate or debt-gate authority semantics.
- Publishing Markdown, changing GitHub permissions, or adding a renderer
  abstraction for formats other than Markdown.

## Decisions

### Persist a workflow-supplied execution context

Health JSON gains a version-2 reporting-evidence envelope with a non-empty
`execution_context` object containing an execution identifier and
condition-set scope.  Each existing Health validation receipt remains the
canonical mode record.  `health --format json` accepts an explicit
`--execution-context`; its report evidence is intentionally unavailable to
`report pr` when no identifier was supplied.

`change report` accepts the same required identifier and emits a version-2
change report containing that identifier plus the mode and condition set that
the two snapshots already proved equal.  The PR reader requires exact
identifier and condition-set equality and exactly one Health receipt for the
change mode.  This permits a Health `all` evaluation to supply its matching
strict or audit receipt, while rejecting a strict-only Health paired with an
audit change report.

An implicit fixed context such as `local` was rejected because it would make
unrelated runs appear compatible.  A derived hash was rejected because an
execution/run identity cannot be reconstructed reliably from the two persisted
outputs without a second source of truth.

### Make availability a strict wire contract at the reader boundary

The reader owns a table of the six known keys and accepted values.  It rejects
duplicates and requires the exact key set.  Inventory, lifecycle, and
applicability use `available` iff their payload is present; topology uses
`available` iff an applicability record carries topology and otherwise
`not_configured`; external evidence follows the same `available` / `not_configured`
rule; findings are always present and therefore `available`.

The writer produces that table from the owning receipts.  The projector relies
on already-validated evidence and maps explicit unavailability or Health
unassessability without filling absent data with empty values.  Accepting
unknown keys for forward compatibility was rejected: this presentation path is
security- and governance-sensitive, so a newer authority schema must be
explicitly taught to the reader before it can be trusted.

### Use canonical source classification for blockers

The projection selects the receipt matching the change report mode.  Its
strict-mode blocking findings, lifecycle records listed by their receipt's
canonical blocking states, and debt-gate facts that are explicitly blocking
form the blocker set.  Audit receipts and diagnostics from external evidence,
baseline configuration, and preflight remain evidence unless their owner marks
them blocking.  Policy weakening is selected through the debt gate's requested
and completed/passed receipt state, not its display strings.

This avoids using aggregate `gate != pass` as a classifier, which loses the
authority that determined each item.

### Keep raw reporting DTOs report-owned and eliminate partial types

PR report input, evidence, projection, and report-owned change projection are
declared in `Core.Reporting`; `Core.Model` has no dependency on `Core.Change`.
The report projection copies only the change fields it needs into a
report-owned view.  Reader parsing is split into non-partial parser
collaborators, Health evidence serialization into non-partial writer
collaborators, and Markdown escaping into a non-partial formatter
collaborator.  This retains the existing Core-to-CLI boundary while complying
with the production partial-type rule.

### Escape per Markdown context and suppress empty sections

`EscapeInlineCode` normalizes control characters and encodes delimiter and
HTML-sensitive characters so untrusted values cannot terminate code spans.
`EscapeMarkdownText` normalizes controls, escapes Markdown punctuation, and
neutralizes HTML/comment delimiters.  Render call sites select one of these
functions rather than a generic line sanitizer.  Bounded sections are emitted
only for non-empty collections; blocker and debt headings are emitted only
when their respective canonical collections contain data or unavailable
evidence must be disclosed.

### Use stable identity set membership for resolutions

The comparator builds one ordinal `HashSet` from current finding identities and
filters baseline findings against it.  This preserves the current ordering and
identity semantics while making resolved-finding projection O(base + current).

## Risks / Trade-offs

- [Older report artifacts become unusable for PR reporting] → Schema versions
  are explicit and `report pr` fails with an actionable unsupported-artifact
  error; standalone historical artifact inspection remains unchanged.
- [CI must pass one context value to both producers] → The option is explicit,
  documented in command help, and covered by handler tests.
- [Strict availability rejects future authority fields] → This is intentional
  fail-closed behavior; schema evolution requires a corresponding reader
  update and version bump.
- [Report-owned models require broad namespace updates] → Preserve typed data
  and adapt only the composition boundary, then prove the self-policy gate.

## Migration Plan

1. Implement version-2 report evidence and change reports with the new CLI
   execution-context option.
2. Update reader, projector, renderer, and targeted tests together so no
   version-1 pair can reach Markdown rendering.
3. Run focused Core/CLI suites, self-policy coverage, API approval, and
   OpenSpec validation; update reviewed snapshots only after the final API is
   intentional.
4. Publish the backward-incompatible artifact requirement with the PR update.
   Rollback consists of reverting the feature-branch commit; no persisted data
   is mutated in place.
