## Context

`ValidationOutcome.Waivers` is the mode-local input to validation gating. In
contrast, `PolicyInventory` is a repository authority for future Health,
report, and badge consumers. Reusing the former for the latter made equal
strict/audit rule counts coexist with different waiver-debt totals.

## Goals / Non-Goals

**Goals:**

- Produce one exact strict/audit/coverage and waiver-debt inventory for the
  selected effective repository policy.
- Preserve current-mode waiver records and gating unchanged on each
  `ValidationOutcome`.
- Reuse #687 lifecycle evaluation for every record; inventory only combines
  its canonical outputs.

**Non-Goals:**

- Change waiver matching, state precedence, expiry, or gating policy.
- Add a new consumer, cache schema, or inventory schema version.
- Combine the existing JSON enrichment passes.

## Decisions

### Evaluate companion modes only when their selected policy contains waivers

Before returning an inventory, the snapshot will complete normal memoized
evaluation for each strict or audit mode containing a selected manual waiver.
It then combines their mode-local `ValidationOutcome.Waivers` records into the
one inventory input. A policy with no waiver in a companion mode does not need
that companion execution: it cannot add lifecycle evidence. This keeps the
repository aggregate exact without reimplementing #687 matching from policy
text.

Executing the companion through the normal snapshot path was chosen over
inferring its state from absent unmatched diagnostics. The latter would falsely
classify a non-executed stale waiver as active. It also preserves cache lookup,
memoization, provenance, and lifecycle behavior.

### Separate current-mode gating from repository inventory evidence

The current mode's `Waivers`, `Passed`, and blocking-waiver calculation remain
the output of its own evaluation. Companion outcomes supply only their
canonical lifecycle records to `PolicyInventory`. The aggregate is then
attached identically to every memoized outcome that participated, so strict and
audit consumers cannot construct competing totals.

### Preserve normal snapshot accounting

Companion work uses the ordinary per-mode snapshot evaluation path. Its cache
behavior and counters therefore reflect actual execution, and a later explicit
request for that mode returns the memoized completed outcome rather than
executing again.

## Risks / Trade-offs

- [A single requested mode can evaluate a waiver-bearing companion mode] →
  restrict companion work to selected modes with manual waivers and preserve
  current-mode gating/output fields.
- [A companion-mode runtime failure can surface while completing repository
  evidence] → use the same normal Core path so a policy that cannot produce a
  trustworthy inventory fails closed rather than emitting a partial authority.
- [Cached prior v1 inventory may contain mode-local debt] → rebuild the
  returned inventory from cached mode-local waiver records, not from the cached
  inventory object.

## Migration Plan

No persisted shape changes. New outcomes and cache reconstructions reuse the
existing `architecture-policy-inventory/v1` fields with corrected repository
semantics. Reverting the commit restores previous execution behavior; no policy
data migration is necessary.

## Open Questions

None.
