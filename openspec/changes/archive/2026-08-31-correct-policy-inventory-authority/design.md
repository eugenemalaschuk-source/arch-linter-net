## Context

`architecture-policy-inventory/v1` is intended to be the single input for
repository Health and later report and badge consumers. Its first projection
was parameterized by the current validation mode, while execution selection
already expands an authored source-set ID to effective aliases. Waiver
lifecycle selection did not share that authored-ID equivalence, allowing a
partial run to execute a rule but omit its waiver debt.

## Goals / Non-Goals

**Goals:**

- Produce one deterministic strict/audit/coverage inventory for the selected
  effective policy, independent of whether the invocation evaluates strict or
  audit findings.
- Reuse the same authored source-set selection identity for execution,
  inventory, and waiver lifecycle evaluation.
- Keep cache, CLI, and Testing as projections of the Core object and approve
  the intentional public API expansion.

**Non-Goals:**

- Add Health, report, or badge consumers.
- Combine the existing CI-artifact JSON enrichment passes; that is a follow-up
  performance improvement.
- Change waiver-state precedence, expiry, or matching semantics.

## Decisions

### Project every effective mode from the shared catalog

The inventory projector will no longer filter descriptors by the invocation
mode. It will preserve each descriptor's own strict/audit mode in the
breakdown, keep coverage as a separate partition, and retain selection and
execution-scope exclusions. This makes a strict or audit result carry the same
repository-level rule authority. Aggregating inventories in a Health consumer
was rejected because it would create a second semantic authority and duplicate
source-set deduplication rules.

### Centralize selected-descriptor identity matching

The waiver lifecycle evaluator will accept a selected descriptor when either
its effective ID or its `ExpansionOrigin.AuthoredContractId` matches a requested
contract ID, using the same case-insensitive semantics as execution. This is
implemented at lifecycle selection rather than after evaluation so stale,
expired, and invalid selected waivers remain blocking canonical records.

### Refresh the approval artifact deliberately

The whole-Core API approval baseline is an additional reviewed contract beyond
the generated public API snapshot. It will be regenerated through its approved
test workflow after code changes and inspected as part of the PR update.

## Risks / Trade-offs

- [Strict output now reports audit controls] → This is the intended v1
  repository-level contract and is pinned with strict/audit parity tests.
- [Authored-ID selection could broaden lifecycle scope] → It broadens only to
  aliases that execution already runs for that same authored contract, and is
  covered by active and blocking waiver tests.
- [More API approval artifact churn] → Regenerate only the reviewed Core
  baseline and verify it matches the assembly surface.

## Migration Plan

The cache shape is unchanged: existing cache entries can remain inventory-free,
and new entries continue to serialize the same v1 object with corrected
contents. If a regression is found, revert the code and approval-baseline
commit; no external persisted-policy migration is required.

## Open Questions

None.
