## Context

Issue #235 defines Release Architecture Forensics theory before implementation.
Downstream tasks need one reviewed source of truth for Git-range identity,
normalization, numeric precision, task independence, graph semantics, ranking,
and cautious report language.

The feature is planned, not shipped, so public MkDocs pages must not present it
as current product behavior.

## Goals / Non-Goals

**Goals:**

- deterministic Git-only evidence and canonical scoring;
- one canonical rename/category model;
- explicit comparable normalization cohorts and numeric precision;
- validated effective profiles and threshold semantics;
- false-positive control for multi-reference task evidence;
- cohort-safe graph centrality and ranking;
- optional .NET enrichment strictly downstream.

**Non-Goals:**

- implementing CLI, policy schema, analyzer, reports, or Roslyn enrichment;
- a second config language or executable;
- formal proof of coupling, merge conflicts, or OCP violations.

## Decisions

### Internal reference plus capability spec

The OpenSpec capability is the testable contract and
`docs/internal/release-forensics.md` is the readable contributor reference.
Public navigation remains unchanged until implementation ships.

### Canonical inputs and logical-file identity

Authored/resolved refs, effective config identity, tool version, normalized
logical paths, authors, task refs, and commit order are canonical. Checkout and
environment presentation data are not.

One unambiguous linear rename chain is one logical file. The last in-range path
is canonical; earlier paths remain aliases. Copy/split/merge/ambiguous relations
stay separate. Primary category derives from the canonical path.

### Comparable normalization cohorts

#237 ignores remove files before score/graph construction. File metrics normalize
within primary-category cohorts; edge metrics within unordered endpoint-category
cohorts. Presentation suppression cannot affect scores.

Because cohort-local normalized values are not a common absolute scale, findings,
pairs, clusters, and candidates remain grouped and rank only within comparable
cohorts. Production is the primary human hotspot ranking.

### Nine-decimal canonical numeric model

Mathematical formulas remain authoritative. Canonical derived reals use:

```text
Q(v) = round-half-to-even(v, 9 decimal places)
```

Components, temporal proximity, edge weights, final scores, and thresholds are
canonicalized before comparison/ranking/serialization. JSON uses exactly nine
fractional digits, invariant culture, no exponent notation. This avoids making
canonical output depend on `Math.Log`, libc, CPU, or intermediate floating-point
representation.

### Validated effective profiles

Weights are finite non-negative base-10 decimals with at most nine fractional
digits. Enabled components are positive, disabled components zero, at least one
is enabled, and each profile sums exactly to `1.000000000`; co-change therefore
requires `alpha + beta = 1.000000000`. Invalid profiles fail rather than being
silently normalized. Missing evidence never alters weights.

### Independent task evidence

Multi-reference commits may contribute ordinary breadth/co-change but cannot by
themselves prove independent work. A task pair requires pair-exclusive commits
on both sides. Temporal proximity uses those pair-exclusive intervals.

For repeated OCP editing, a task participating in multiple independent pairs
unions its pair-exclusive commit sets and deduplicates by SHA before counting
repeated edits. This prevents partner-count multiplication.

### Metric-bound cluster thresholds

A significance threshold applies only to canonical `CombinedCoChange`, uses
inclusive `>=`, and lies in `[0,1]`. Clusters are connected components of
qualifying edges built independently per endpoint-category cohort. No threshold
means no inferred clusters.

### Cohort-safe centrality

Endpoint-cohort-normalized edge scores cannot be summed into one file centrality
value because a file may have edges from several endpoint-category cohorts.
Therefore centrality uses raw incident evidence first:

```text
IncidentCommitDegree(f) = Σ CommitCoChange(f,n)
IncidentTaskDegree(f)   = Σ TaskCoChange(f,n)
IC_f = normalized IncidentCommitDegree inside f's primary-category cohort
IT_f = normalized IncidentTaskDegree inside f's primary-category cohort
K_f  = Q(alpha*IC_f + beta*IT_f)
```

This preserves category-local comparability without mixing separately normalized
edge scales. Bottleneck and OCP scoring reuse this `K_f`.

Alternative considered: sum canonical `CombinedCoChange` over incident edges.
Rejected because production-production and production-tests edge scores may have
been normalized against different populations.

### Bounded role-token evidence

Role hints use deterministic identifier-boundary tokenization, invariant-lowercase
exact equality, stable token reporting, and a bounded default score contribution.
Substring/glob/regex matching is excluded.

### Optional .NET enrichment stays downstream

Project/namespace/type facts enrich completed file findings but cannot remove,
reorder, or manufacture Git evidence.

## Risks / Trade-offs

- Missing/inconsistent task refs understate signals → preserve zero semantics and
  report the limitation.
- Category-local normalization prevents noise domination → cross-category scores
  cannot be interpreted as one global ranking.
- Different math libraries may vary internally → only correctly rounded
  nine-decimal canonical values participate in decisions/output.
- Multi-reference commits may represent legitimate shared work → preserve
  ordinary breadth while requiring pair-exclusive evidence for parallel pressure.
- Cluster threshold choice influences grouping → bind it to one canonical metric
  and keep pair evidence available.
- Role tokens may overfit names → exact bounded matching and caveats.

## Migration Plan

1. Archive this theory contract into the main capability.
2. #236 implements deterministic Git ingestion, logical identity, and task refs.
3. #237 implements validated config, ignores, categories, thresholds, profiles.
4. #238/#239 implement canonical hotspot/co-change evidence.
5. #240/#241 implement independent-task, cohort-safe centrality, and OCP pressure.
6. #242/#243 add optional enrichment and stable grouped reports.

No deployment or rollback action is required because this change adds no runtime
behavior.

## Open Questions

- None for the first deterministic profile. Changes to defaults, numeric scale,
or evidence semantics require reviewed specification work with migration notes.
