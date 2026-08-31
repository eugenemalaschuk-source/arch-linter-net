## Context

The effective policy loader and `ArchitectureContractCatalog` already resolve
imports, conditions, and source-set expansion into the executable contracts
used by validation.  #687 also supplies one lifecycle record for every manual
waiver, with the authoritative state precedence, target identity, and portable
provenance.  CLI and Testing currently expose those records but have no common
policy-level inventory for downstream Health, report, and badge consumers.

## Goals / Non-Goals

**Goals:**

- Produce one versioned Core inventory for the exact validation mode and
  selected effective contract scope.
- Count authored/effective controls once, including one count for all aliases
  of a source-set-expanded contract.
- Summarize every canonical current waiver record without re-parsing YAML,
  matching findings, or evaluating dates.
- Carry the identical object through validation, cache reconstruction, Testing,
  and CLI human/JSON result projections.

**Non-Goals:**

- Add a rule evaluator, waiver schema, matcher, expiry clock, baseline debt
  lifecycle, or policy-weakening comparison.
- Reinterpret selector/exclusion syntax as waiver debt.
- Define Architecture Health, PR-report, badge, or any arithmetic health score.

## Decisions

### Project from effective execution facts

An internal `ArchitecturePolicyInventoryProjector` will receive the loaded
effective document, the validation mode/selected contracts, and the existing
waiver lifecycle records. It will use `ArchitectureContractCatalog`, the same
catalog used by execution, rather than YAML traversal or finding counts.

The stable rule identity is the authored contract ID when source-set expansion
provides one; otherwise it is the effective contract ID, falling back to the
contract name.  Identity is scoped by mode/group/family.  This deduplicates
source-set aliases while retaining genuinely separate controls. Optional-empty
expansions have no executable effective control and are not counted.

Alternative considered: count YAML contract entries. Rejected because it would
diverge from imports, conditions, and effective source-set expansion.

### Keep breakdowns a partition, not a second total

The inventory headline is `effective_rule_count`. Its deterministic breakdown
places coverage-family controls in `coverage`; non-coverage controls are
placed in `strict` or `audit`. The three counts partition the headline so they
cannot inflate it by assigning a coverage control both a mode and a family
count.

Alternative considered: independently report mode and family totals. Rejected
for v1 because overlapping totals invite consumers to sum them incorrectly.

### Consume, never classify, waiver lifecycle records

`ignore_debt.total` is the number of projected current manual waiver records.
Each record contributes to exactly one state counter based on #687's already
computed state. The inventory retains the sorted lifecycle records as the
stable drill-down reference. Baseline-imported ignores never enter those
records, and no ordinary findings or structural exclusions are inspected.

An unknown future lifecycle state is rejected rather than silently treated as
zero debt. This makes a future #687 state an explicit inventory-versioning
decision.

Alternative considered: recompute match and expiry state from policy fields.
Rejected because #687 is the only lifecycle authority and reimplementation
would create divergent states.

### Preserve absent evidence as absent

Actual validation always attaches an inventory. Compatibility constructors and
old cache payloads may have no inventory, in which case CLI/Testing preserve
that absence instead of emitting a synthetic zero-count object. Future Health
consumers can therefore recognize missing evidence as unassessable.

The active packaged analysis-cache writer schema is extended with the optional
inventory object, while its frozen historical resources remain unchanged. A
real store round-trip validates the writer payload against that exact packaged
schema.

### Render through existing result sinks

The validation result's human formatter adds compact policy-rule and waiver
debt lines. JSON adds a `policy_inventory` property containing the versioned
Core object. The Testing adapter exposes the same object. No workflow script
or downstream renderer is asked to parse YAML or recount controls/waivers.

## Risks / Trade-offs

- [Public API growth] → Add focused API approval and reviewed public-API
  snapshot checks.
- [Result-cache compatibility] → Persist the inventory in the existing cache
  payload and treat older payloads as missing evidence, never zero.
- [Source-set identity mistakes] → Cover aliases and optional-empty expansion
  with projector tests using the same catalog behavior as execution.
- [New lifecycle states] → Fail closed until the inventory contract explicitly
  supports the state.

## Migration Plan

The projection is additive. Existing policy files need no migration because it
only consumes their current effective contracts and #687's compatibility
lifecycle records. Consumers may adopt `policy_inventory` when present and
must not substitute an all-zero value when it is absent.
