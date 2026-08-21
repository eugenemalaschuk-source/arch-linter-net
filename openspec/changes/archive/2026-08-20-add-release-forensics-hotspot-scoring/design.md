## Context

Canonical ingestion already produces ordered `LogicalFile` events and commit
metadata, while `history_analysis` already validates path classification and the
effective hotspot profile. Hotspot scoring must join those two evidence sources
without revisiting Git objects, task extraction, metadata decoding, path
lifetimes, or rename decisions.

## Goals / Non-Goals

**Goals:**

- Produce immutable, deterministic in-memory hotspot findings from a successful
  ingestion result and validated history-analysis configuration.
- Preserve both raw evidence and nine-decimal canonical components/score.
- Apply ignore and category classification before category-local normalization,
  then order findings with the semantic profile's total-order tie-break.

**Non-Goals:**

- Change Git ingestion, canonical event construction, configuration validation,
  CLI command shape, or successful-report schema.
- Implement co-change, bottleneck, OCP, Roslyn enrichment, Markdown, or
  canonical JSON reporting.
- Introduce path-lifetime segmentation or reconstruct metadata/task/ref evidence.

## Decisions

### Use a focused Core scoring service and result models

Add a concrete History analysis service that accepts `HistoryIngestionResult`
and `HistoryAnalysisConfiguration`, returning category-grouped hotspot findings.
This is the smallest boundary that allows #238 to be independently tested while
leaving CLI presentation for #243. Integrating calculations into Git ingestion
would conflate canonical event creation with configurable derived evidence;
putting it in the CLI would violate the Core-owned analysis boundary.

### Derive metrics only by joining existing canonical evidence

For each event commit ID, the scorer looks up the existing `CommitEvidence` and
uses its canonical TaskKeys, canonical author, and exact `BigInteger` committer
epoch. `LogicalFile` continues to supply distinct event commit count and summed
churn. This honors the upstream canonicalization boundary and prevents any new
Git/API or host-calendar dependency.

### Keep raw integer evidence exact; quantize derived decimals

Temporal span remains a `BigInteger`; only normalized components and weighted
scores become decimal values, rounded half-to-even to nine places. Churn's
logarithmic normalization uses the specified mathematical `log(1+x)` ratio,
then immediately quantizes. All-zero cohorts yield zero components and scores.

### Group and rank after ignore/classification

The existing `HistoryPathClassifier` filters ignored paths before all score
populations. Retained files normalize only against the maximum values in their
own primary category. Results group categories in the fixed category order and
sort inside each group by score, TaskKey spread, churn, commit count, then scalar
path comparison. Production remains first as the primary human-facing group.

## Risks / Trade-offs

- [Floating logarithm implementation near a rounding boundary] → Quantize once
  at the canonical boundary and cover scoring goldens plus all-zero behavior.
- [Ingestion event IDs cannot be joined to metadata] → Treat it as an internal
  invariant and fail fast rather than silently inventing missing evidence.
- [Later report work needs more fields] → Store explicit evidence/limitation
  properties now, but defer serialization and presentation decisions to #243.
- [Same-path reuse can over-aggregate] → Retain the inherited pathname-reuse
  limitation in each finding rather than splitting lifetimes locally.

## Migration Plan

The scorer is additive and internal. No policy migration, CLI switch, stored
artifact, or rollback procedure is required; callers may adopt the new Core
result before report generation is added.

## Open Questions

None. The issue and existing release-forensics specification settle the relevant
semantic choices.
