## Context

Issue #235 establishes the theory contract for Release Architecture Forensics
before implementation begins. The current product evaluates present-state
architecture contracts; the planned feature evaluates evidence accumulated in
an explicit Git range. Downstream implementation tasks need one source of truth
for input identity, normalization populations, numeric canonicalization, scoring,
ranking, and cautious report language.

The feature does not yet exist. Public MkDocs pages must describe implemented
product behavior, while this issue needs durable contributor-facing guidance.

## Goals / Non-Goals

**Goals:**

- Define deterministic Git-only canonical evidence and score semantics.
- Fix the initial score profiles, normalization populations, numeric precision,
  ordering, and evidence vocabulary consumed by #236 through #243.
- Preserve a clean boundary between Git/history core, optional .NET enrichment,
  schema-backed configuration, and presentation.
- Prevent independent implementations from making different legitimate choices
  for rename identity, multi-reference task evidence, thresholds, or ranking.

**Non-Goals:**

- Implement Git ingestion, CLI parsing, policy schema, scoring, reports, or
  Roslyn enrichment.
- Create a second configuration language, executable, or public documentation
  promise for unimplemented behavior.
- Treat heuristic output as formal proof of coupling, merge conflicts, or OCP
  violations.

## Decisions

### Keep theory in the capability spec and internal contributor reference

The `release-architecture-forensics` capability is the testable contract;
`docs/internal/release-forensics.md` is the readable formula-oriented reference.
The feature stays outside public MkDocs navigation until implementation ships.

### Resolve canonical inputs before analysis

Canonical identity records authored refs, resolved commit IDs, effective
history-analysis configuration identity, and tool version. Logical paths,
normalized authors, ordered task references, and deterministic commits are
canonical evidence. Checkout paths, generated timestamps, locale, timezone, and
process environment are not.

### Give each rename chain one canonical to-side path

One unambiguous linear rename chain is one logical file. Its canonical path is
the last in-range occurrence, including a deleted path when deletion is last.
Earlier paths remain aliases. Copy/split/merge/ambiguous relations stay separate.
Primary category comes from the canonical path.

### Normalize after analysis filtering and by comparable cohort

#237 ignore rules remove files before graph/score construction. Presentation
suppression is downstream. File metrics normalize within primary-category
cohorts. Edge metrics normalize within unordered endpoint-category cohorts.
This prevents generated/docs/test/build volume from setting production maxima.

The consequence is explicit: normalized values from different cohorts are not
one common scale. File, pair, cluster, and candidate rankings therefore remain
cohort-local. Production is the primary human-facing hotspot ranking; other
categories are separate groups in fixed canonical order.

Alternative considered: globally rank category-local scores. Rejected because a
`0.95` docs score and `0.80` production score are relative to different maxima.

### Canonicalize derived real numbers at nine decimal places

The mathematical formulas remain the authority, including logarithmic churn.
Canonical derived real values use:

```text
Q(v) = round-half-to-even(v, 9 decimal places)
```

Normalized components, temporal proximity, combined edge weight, final scores,
and numeric thresholds are canonicalized before comparisons/ranking/JSON.
Canonical JSON emits exactly nine fractional digits, invariant culture, and no
exponent notation.

This deliberately specifies the correctly rounded mathematical result rather
than a specific `Math.Log`, libc, CPU, or intermediate floating representation.
Implementations may use different internal algorithms but must converge on the
same canonical fixed-scale value.

### Validate effective profiles instead of repairing them at runtime

Effective weights are finite non-negative base-10 decimals with at most nine
fractional digits. Enabled components are positive, disabled components are zero,
at least one is enabled, and each profile sums exactly to `1.000000000`. For
co-change, `alpha + beta = 1.000000000`.

Invalid profiles fail validation. Missing evidence never triggers implicit
renormalization. This keeps a score's meaning stable between ranges.

### Separate ordinary task breadth from independent-work evidence

A commit may reference multiple issues and contribute ordinary task spread or
task co-change to each. It cannot by itself establish independent workstreams.
Two task refs become an independent pair for a file only when each has at least
one pair-exclusive commit touching that file. Temporal proximity uses those
pair-exclusive intervals.

### Make repeated-edit aggregation total for multi-pair tasks

A task may be independently paired with several other tasks. For one task `t`,
collect every pair-exclusive commit set against each independent partner, union
those sets, deduplicate by SHA, then count qualifying commits after the first.
The resulting `Repeated_f(t)` is summed over participating task refs.

Alternative considered: sum repeated edits independently per pair. Rejected
because one commit could then be multiplied by the number of partners.

### Bind cluster thresholds to one canonical metric

A significance threshold applies only to canonical `CombinedCoChange`, uses an
inclusive `>=` comparison, and is restricted to `[0,1]`. Clusters are connected
components of qualifying edges built independently inside each endpoint-category
cohort. No configured threshold means no inferred cluster cutoff.

Alternative considered: allow threshold implementations to choose raw commit,
task, or component weights. Rejected because identical config could then produce
different cluster membership and refactoring candidates.

### Use bounded deterministic role-token proxies

Role/name hints tokenize the canonical file stem at deterministic identifier
boundaries and use invariant-lowercase exact token equality. Substring/glob/regex
matching is excluded. Matched tokens are reported and the default role-hint
contribution remains 10%.

### Keep optional .NET enrichment strictly downstream

Git evidence remains useful if a C# file cannot be parsed. Project, namespace,
and type data enrich completed file-level findings but cannot remove, reorder,
or manufacture core evidence.

## Risks / Trade-offs

- [Task references may be absent/inconsistent] → missing metrics are zero;
  effective weights remain unchanged and the limitation is reported.
- [Multi-reference commits] → ordinary breadth is preserved, but independent
  work evidence requires pair-exclusive commits.
- [Category-local normalization] → scores cannot be globally compared; reports
  use explicit category/cohort groups instead.
- [Floating/log implementation variance] → only correctly rounded nine-decimal
  canonical values participate in output and decisions.
- [High churn may be mechanical] → path categories and #237 ignore rules provide
  deterministic noise control.
- [Name hints can overfit vocabulary] → exact bounded tokens and a 10% default cap.
- [Co-change clusters can broaden] → threshold is explicit, metric-bound,
  inclusive, and cohort-local; pair evidence is always retained.

## Migration Plan

1. Archive this theory contract into the main OpenSpec capability.
2. #236 implements Git-range ingestion, canonical rename identity, and task refs.
3. #237 supplies validated `history_analysis` configuration, category rules,
   ignores, thresholds, and effective profiles.
4. #238/#239 implement hotspot/co-change evidence using canonical numeric and
   cohort semantics.
5. #240/#241 consume independent-task and repeated-edit evidence.
6. #242/#243 add optional enrichment and stable grouped reports without silently
   changing the theory contract.

No deployment or rollback action is required because this change adds no runtime
behavior.

## Open Questions

- None for the first deterministic profile. Later changes may revise defaults,
  numeric scale, or evidence semantics only through reviewed specification work
  with migration notes.
