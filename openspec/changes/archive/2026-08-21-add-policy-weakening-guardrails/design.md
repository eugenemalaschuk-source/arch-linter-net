## Context

Issue #118 exposes a versioned, deterministic, typed projection of the
effective policy and its authored/effective provenance.  Issue #117 exposes
complete analysis snapshots, but deliberately does not contain per-contract
selector membership.  #119 must compare separately produced base and current
policy states without building another loader, selector engine, or candidate
reanalysis path.

## Goals / Non-Goals

**Goals:**

- Make a separately produced base/current policy-context comparison fail closed
  and deterministic.
- Classify only policy changes whose weakening direction is proved by the
  typed effective inputs; retain a bounded, visible result for selector changes
  whose affected subjects cannot be proved.
- Preserve authored/effective provenance and a stable, non-display-text finding
  identity in every format.
- Provide an explicit policy command and severity-controlled CI exit decision
  without changing ordinary strict/audit evaluation.

**Non-Goals:**

- Loading a historical policy from the current checkout, generic candidate
  policy simulation, or automatic policy editing.
- Replacing #66 consistency, #57/#101 coverage, #117 snapshots, #357
  baseline identity, or #121's combined debt gate.
- Claiming complete selector membership from a change snapshot or from a green
  validation result.

## Decisions

### Compare exported contexts, not YAML or live repository paths

The CLI accepts explicit base and current `architecture-policy-context/v2`
JSON files, generated in their respective repository states with `policy
context --format json`.  Core validates kind, version, required identity and
collections, and compatible policy name/version before comparison.  Context
formatting/order-only edits therefore compare as a no-op, and invalid or
incomplete input is an error rather than a clean result.

This makes the historical-state trust boundary explicit.  Accepting two policy
paths would make it too easy to read both sides from the current worktree.

### A focused, typed Core guardrail model owns normalized findings

`ArchLinterNet.Core.PolicyWeakening` will contain a public comparison request,
result, finding, evidence, and formatter.  A finding contains a stable kind,
control identity, canonical base/current values, both provenance locations,
optional reviewed reason, configured severity, and one classification:
`semantic` or `impact_not_proven`.  Its identity is built from those typed
identifiers, never human display text.

The comparator reuses policy-context contract facts, source-set expansion,
exceptions, selectors, and provenance.  It recognizes only these proven
directions:

- removal or same-family/same-ID strict-to-audit downgrade;
- removal of resolved source-set members and newly matched subtractive
  exclusions;
- removal from explicit forbidden inventories and addition to explicit
  allow-only inventories;
- newly universal ignored/exemption matchers.

Changed type/role/CEL/public-surface selectors, non-ordered predicates, and
heuristic Shared/Common/Utils-style exemptions create an `impact_not_proven`
finding.  The latter is intentionally a bounded suspicion, not an assertion
about every affected subject.

### Optional membership evidence is explicitly bound to its context

The Core API also accepts an optional narrow versioned membership evidence
projection.  It is bound to the canonical JSON digest of its corresponding
policy context and carries stable assembly/type subject identities per
contract.  The comparator uses it only when both sides are complete, supported,
digest-matched, and identify the same control; otherwise it emits the bounded
selector finding.  #117 snapshots are never coerced into this shape.

This is a typed seam for output produced by the ordinary authoritative
evaluator; it is not a YAML-derived selector simulator or a new analysis mode.

### Severity is a policy schema setting and does not erase evidence

`analysis.policy_weakening` accepts `error`, `warn`, or `off` and is projected
in the current policy context.  The comparison uses the current setting for
all findings: `error` fails the guardrail command, while `warn` and `off` stay
visible in output without failing it.  Existing schema-backed contract and
ignore `reason` fields are copied as reviewed rationale evidence; no universal
free-form migration field is introduced.

### One policy subcommand projects all formats

`arch-linter-net policy weakening --base-context <path> --current-context
<path> --format human|json|sarif` reads only the supplied artifacts.  Human,
JSON, and SARIF are rendered from one normalized result.  It returns 0 when
there are no `error` findings, 1 when any `error` finding exists, and 2 for
arguments, I/O, or comparison-input failures.

The command remains under the existing instance-based policy module; Core has
no dependency on CLI.  #121 can consume the typed result without turning it
into baseline debt.

## Risks / Trade-offs

- [Context v2 is not membership evidence] → bounded selector results are the
  default; exact subjects require an explicitly bound projection.
- [A broad rule list can overstate semantic direction] → use a conservative
  family/fact allow-list and emit suspicion for all unmatched shapes.
- [Historical artifacts can be stale or hand-edited] → validate complete
  structure, kind/version, identity compatibility, and membership digest;
  fail closed on invalid evidence.
- [New global severity could affect normal validation] → it is read only by
  the explicit weakening workflow; ordinary validation ignores it.
- [SARIF consumers expect stable rule IDs] → derive them from the normalized
  weakening kind, with evidence in result properties.

## Migration Plan

1. Produce base and current policy contexts in their respective checkouts.
2. Run the explicit weakening command in CI or review automation.
3. Start with `analysis.policy_weakening: warn` for a reviewed migration if
   needed; retain the emitted evidence and reason, then promote to `error`.
4. Rollback means removing the explicit workflow invocation; no baseline or
   normal validation behavior has been mutated.

## Open Questions

None.  Exact membership evidence is deliberately an optional typed input until
the ordinary evaluator exposes the necessary per-contract memberships.
