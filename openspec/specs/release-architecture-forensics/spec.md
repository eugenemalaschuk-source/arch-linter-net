# release-architecture-forensics Specification

## Purpose

Define the deterministic Git-range evidence, scoring, finding, recommendation,
and report semantics that govern Release Architecture Forensics implementation.

## Requirements

### Requirement: Explicit deterministic analysis identity
Release Architecture Forensics SHALL analyze an explicit Git range whose
`from` ref is exclusive and whose `to` ref is inclusive. Both refs SHALL be
resolved before analysis; a missing, ambiguous, or unresolvable ref SHALL fail
closed with a diagnostic rather than selecting a default branch or range.

The canonical analysis identity SHALL contain the authored refs, their resolved
commit IDs, the effective `history_analysis` configuration identity, and the
tool version. It SHALL use normalized repository-relative logical paths and
shall not contain an absolute checkout root, local timezone, generated-at
timestamp, or other environment-dependent presentation data. Uncommitted
working-tree state SHALL not alter Git-only evidence.

Commit records SHALL use a stable ascending order of committer UTC timestamp
followed by ordinal commit SHA. A normalized author identity SHALL be the
trimmed, invariant-lowercase email when one is present; otherwise it SHALL be
the trimmed, invariant-lowercase author name; an absent value SHALL be the
literal `unknown`. Task/issue references SHALL be extracted, deduplicated, and
ordered deterministically under the effective configuration.

#### Scenario: Missing explicit ref
- **WHEN** an analysis request names a `from` or `to` ref that cannot be resolved
- **THEN** the analysis fails with a non-success result and does not substitute another ref

#### Scenario: Equivalent execution environments
- **WHEN** the same repository objects, resolved refs, effective configuration, and tool version are analyzed from two checkout roots or timezones
- **THEN** their canonical results have identical identities, logical paths, findings, rankings, and canonical JSON

#### Scenario: Empty range
- **WHEN** an explicit range resolves to no commits
- **THEN** the result succeeds with an explicit empty-range summary and deterministic empty/zero findings rather than an undefined result

### Requirement: Canonical logical-file identity and path classification
The Git/history core SHALL model an analyzed range, commits, normalized authors,
repository-relative logical file paths and rename chains, extracted task/issue
references and task episodes, path categories, file hotspots, co-change edges
and clusters, parallel-development bottlenecks, OCP-pressure points, and
evidence-backed refactoring candidates.

A logical file SHALL represent one unambiguous linear rename chain. Its
canonical logical path SHALL be the normalized repository-relative path at the
last in-range occurrence of that chain, including the deleted path when the last
occurrence is a deletion. Earlier paths SHALL remain ordered aliases/evidence but
SHALL NOT create independent file scores. A copy, split, merge, or otherwise
ambiguous rename relation SHALL NOT be collapsed: each path remains a separate
logical-file identity unless Git evidence establishes one unambiguous chain.

Commit file entries SHALL retain status, additions, deletions, observed path,
and logical-file identity. Churn for file `f` in range `r` SHALL be
`Σ(additions(f, commit) + deletions(f, commit))` over its logical identity.
Churn is change volume, not a complexity measure.

Every logical file SHALL have exactly one primary category, derived by applying
the effective #237 classifier to its canonical logical path: `production`,
`tests`, `docs`, `generated`, `build_ci`, `samples_examples`, or `unknown`.
Alias paths and their classifications MAY be retained as evidence, but they
SHALL NOT override the primary category or ranking path. The schema-backed,
bounded matcher, ignore model, category overrides, and thresholds belong to the
normal policy model owned by #237; this capability SHALL NOT define a second
configuration authority. `unknown` paths SHALL remain visible and shall not be
silently dropped.

The canonical category order SHALL be:

1. `production`;
2. `tests`;
3. `docs`;
4. `generated`;
5. `build_ci`;
6. `samples_examples`;
7. `unknown`.

That order is a serialization/group-order rule, not a claim that scores from
separate category cohorts are numerically comparable.

#### Scenario: Rename-chain normalization
- **WHEN** Git evidence unambiguously identifies `src/Old.cs` renamed to `tests/New.cs` within the analyzed range
- **THEN** both observations contribute to one logical file whose canonical path is `tests/New.cs` and whose primary category is derived from that canonical path

#### Scenario: Ambiguous rename evidence
- **WHEN** Git evidence represents a copy, split, merge, or otherwise ambiguous path relationship
- **THEN** the analyzer does not invent one logical identity and keeps the affected paths separate

#### Scenario: Unknown path category
- **WHEN** a canonical logical path matches no effective path category rule
- **THEN** the result records the logical file as `unknown` and preserves it in applicable evidence output

### Requirement: Total normalization, canonical numbers, and deterministic populations
For every non-negative component population, the mathematical normalizers SHALL be:

```text
normalized(x) = 0                              when max(population) = 0
                x / max(population)            otherwise

normalized_log(x) = 0                          when max(population) = 0
                    log(1 + x) / log(1 + max)  otherwise
```

All-zero and empty populations SHALL return finite `0` values; they SHALL NOT
produce NaN, Infinity, exceptions, or implementation-specific fallbacks. A
missing optional evidence family, including absent task references, contributes
raw `0` to each dependent component. The system SHALL NOT renormalize remaining
component weights because an evidence family is absent.

Canonical numeric values SHALL use a fixed scale of nine decimal places. Define:

```text
Q(v) = round-half-to-even(v, 9 decimal places)
```

The normalizer expressions above are mathematical real-valued definitions.
Implementations MAY use any higher-precision or equivalent algorithm internally,
but every normalized component, temporal proximity, combined edge weight, final
score, configured numeric threshold, and other canonical derived real value SHALL
be reduced to `Q(v)` before it participates in threshold comparison, ranking, or
canonical serialization. Exact half-way cases SHALL use round-half-to-even.
Canonical JSON SHALL serialize those values in invariant culture with exactly
nine fractional digits and without exponent notation. Raw integral evidence
such as commit count, churn, task count, author count, and day gaps remains exact
integer data.

Ignore rules owned by #237 are analysis filters: ignored logical files SHALL be
removed before score populations and graph evidence are built. Presentation-only
suppression SHALL NOT change canonical scores. File-level metrics, including
hotspot, bottleneck, and OCP components, SHALL be normalized against retained
logical files in the same primary path category. Co-change edge components SHALL
be normalized against retained edges with the same unordered endpoint-category
pair. The endpoint-category pair SHALL itself be ordered by the canonical category
order above. This category-cohort rule prevents generated/docs/test/build volume
from setting the maximum for production findings while keeping each cohort
internally comparable.

#### Scenario: Canonical numeric boundary
- **WHEN** two implementations evaluate the same mathematical component using different internal floating-point or logarithm implementations
- **THEN** they emit and compare the same nine-decimal `Q(v)` canonical value

#### Scenario: Category isolation
- **WHEN** a retained generated file has much higher churn than every retained production file
- **THEN** it does not change the production churn normalization maximum because generated and production files use separate category cohorts

#### Scenario: Ignored file
- **WHEN** the effective policy ignores a logical file
- **THEN** that file does not participate in normalization populations, graph vertices, graph edges, findings, or candidates

### Requirement: Effective scoring configuration
Each run SHALL have one validated effective scoring configuration. The initial
default profiles are: hotspot `(commit .30, churn .25, task .25, author .10,
temporal .10)`; bottleneck `(independent_task .35, author .15, temporal .20,
degree .20, centrality .10)`; OCP pressure `(independent_task .40, centrality
.25, repeated_episode_edit .25, role_hint .10)`; and combined co-change
`(commit .75, task .25)`.

#237 MAY make those weights configurable. Every configured weight SHALL be a
finite non-negative base-10 decimal with at most nine fractional digits. Within
each score profile, every enabled component SHALL have weight greater than zero,
every disabled component SHALL have weight exactly zero, at least one component
SHALL be enabled, and the effective weights SHALL sum exactly to
`1.000000000` at the canonical fixed-point scale. The co-change invariants are
therefore `alpha >= 0`, `beta >= 0`, and `alpha + beta = 1.000000000`.
Configurations violating these invariants SHALL fail validation rather than be
silently normalized. Missing evidence SHALL never alter effective weights at
runtime.

#### Scenario: Invalid configured weight sum
- **WHEN** configured hotspot, bottleneck, OCP, or co-change weights do not sum exactly to `1.000000000`
- **THEN** configuration validation fails and analysis does not silently rescale the profile

#### Scenario: Intentional component disablement
- **WHEN** a component is explicitly disabled by validated configuration
- **THEN** its effective weight is exactly zero and the remaining explicit weights already form a valid `1.000000000` profile

### Requirement: Deterministic hotspot evidence
For every retained logical file `f`, using effective hotspot weights
`w_c,w_h,w_t,w_a,w_r`, the system SHALL compute canonicalized components:

```text
C_f = Q(normalized(commit_count(f)))
H_f = Q(normalized_log(churn(f)))
T_f = Q(normalized(distinct_task_references(f)))
A_f = Q(normalized(distinct_normalized_authors(f)))
R_f = Q(normalized(temporal_span_seconds(f)))

HotspotScore(f) = Q(w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f)
```

The default hotspot weights are `.30,.25,.25,.10,.10` respectively.
`temporal_span_seconds(f)` SHALL be the non-negative difference between the
latest and earliest UTC Unix commit timestamps touching `f`; a one-commit file
has span `0`. This is a persistent-edit signal, not a wall-clock recency value.
Each finding SHALL retain its raw metrics, canonical normalized components, and
effective weights.

Hotspot rankings SHALL be independent per primary-category cohort. Scores from
different cohorts SHALL NOT be compared to produce a single global numeric
ranking. The default human-facing `top hotspots` section SHALL mean production
hotspots; non-production hotspot rankings SHALL be reported in separate category
sections when included. Canonical JSON SHALL serialize category groups in the
canonical category order and findings within each group by the stable ordering
specified below.

#### Scenario: Cross-category scores
- **WHEN** a docs hotspot has score `0.950000000` and a production hotspot has score `0.800000000`
- **THEN** the report does not claim the docs finding outranks the production finding because their scores came from different normalization cohorts

### Requirement: Deterministic co-change evidence and clusters
The co-change graph SHALL be weighted, undirected, and contain one vertex per
retained logical file in the range. For distinct logical files `a` and `b`,
using effective co-change weights `alpha,beta`:

```text
CommitCoChange(a,b) = count(commits containing both a and b)
TaskCoChange(a,b)   = count(distinct tasks whose episodes contain both a and b)

CommitComponent(a,b) = Q(normalized(CommitCoChange))
TaskComponent(a,b)   = Q(normalized(TaskCoChange))
CombinedCoChange(a,b) = Q(alpha*CommitComponent + beta*TaskComponent)
```

The default co-change weights are `alpha=.75` and `beta=.25`. Absent task
evidence contributes zero without changing those effective weights. An edge
pair SHALL be stored in ascending canonical logical-path order.

A co-change significance threshold, when configured, SHALL be a canonical
nine-decimal value in `[0.000000000,1.000000000]` and SHALL apply specifically
to canonical `CombinedCoChange`. An edge qualifies when
`CombinedCoChange >= threshold`; equality is inclusive. No raw commit count,
raw task count, or individual normalized component SHALL be substituted for this
comparison.

Clusters SHALL be constructed independently per unordered endpoint-category
cohort. Within one cohort, the qualifying graph contains exactly the retained
edges whose canonical combined weight meets the threshold, and a cluster is a
connected component of that qualifying graph containing at least two vertices.
When no significance threshold is effective, pair evidence SHALL remain but the
cluster collection SHALL be empty rather than using an inferred cutoff.

Pair rankings and cluster rankings SHALL also be independent per endpoint-category
cohort; scores or aggregate weights from different endpoint-category cohorts
SHALL NOT be compared as one global ranking. Cohort groups SHALL serialize in
canonical endpoint-category order.

#### Scenario: Threshold equality
- **WHEN** an edge has canonical `CombinedCoChange = 0.600000000` and the effective significance threshold is `0.600000000`
- **THEN** the edge qualifies for cluster construction because the comparison is inclusive

#### Scenario: Co-change without tasks
- **WHEN** two files change together in commits but no task references are extracted
- **THEN** their commit evidence remains, task evidence is zero, and combined co-change uses the effective weights without renormalization

### Requirement: Independent task evidence for parallel-development signals
A task episode SHALL be the commits linked to one extracted task/issue reference.
A single commit MAY reference multiple tasks and MAY contribute ordinary task
spread and task-level co-change evidence to each referenced task; that alone
SHALL NOT prove that those task references represent independent workstreams.

For a logical file `f`, two task references `x` and `y` form an independent task
pair only when both sides have pair-exclusive evidence: at least one commit
touching `f` references `x` but not `y`, and at least one commit touching `f`
references `y` but not `x`. Commits that reference both `x` and `y` SHALL NOT be
used to establish independence or temporal overlap/proximity for that pair.

`IndependentTaskSpread(f)` SHALL be the number of task references that
participate in at least one independent pair for `f`. For an independent pair,
each pair-side interval SHALL be built from that side's pair-exclusive commits.
`days_between` SHALL be zero when those pair-exclusive UTC intervals overlap;
otherwise it SHALL be the ceiling of the positive UTC interval gap in days.

```text
TemporalProximity(x,y) = Q(1 / (1 + days_between(x,y)))
```

The raw file temporal value is the maximum canonical proximity across independent
pairs, or `0.000000000` when no independent pair exists.

#### Scenario: One multi-reference commit
- **WHEN** the only commit touching a file references both `#101` and `#102`
- **THEN** ordinary task spread may record both references, but independent task spread, temporal proximity, and repeated-episode-edit evidence are all zero

### Requirement: Deterministic bottleneck and OCP-pressure evidence
For bottlenecks, `T_f` SHALL be `Q(normalized(IndependentTaskSpread(f)))`, `A_f`
shall be canonical normalized author spread, `O_f` SHALL be canonical normalized
independent-task temporal proximity, `D_f` SHALL be canonical normalized
distinct-neighbor degree, and `K_f` SHALL be canonical normalized weighted degree
(the sum of canonical combined co-change weights). Using effective bottleneck
weights `b_t,b_a,b_o,b_d,b_c`:

```text
BottleneckScore(f) = Q(b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f)
```

For OCP pressure, `T_f` SHALL also use canonical normalized independent task
spread. Repeated independent editing SHALL use the following total definition.
For task reference `t`:

```text
Partners_f(t) = { u : (t,u) is an independent pair for f }
PairExclusive_f(t,u) = { commit c touching f : c references t and not u }
Qualifying_f(t) = union(PairExclusive_f(t,u) for u in Partners_f(t)), deduplicated by commit SHA
Repeated_f(t) = max(|Qualifying_f(t)| - 1, 0)
E_f = sum(Repeated_f(t) for every t with Partners_f(t) non-empty)
```

A commit may therefore qualify for more than one independent pair for the same
task reference, but SHALL be counted at most once for that task after the union
is deduplicated by SHA. The same commit MAY count once for two different task
references when it independently qualifies for each reference. If no independent
pair exists, `E_f` is zero. `E_f` is normalized in the file's primary-category
cohort before scoring.

For role hints, the normalized file stem SHALL be the canonical file name without
its final extension, tokenized deterministically by splitting on non-alphanumeric
characters, lower-to-upper camel/Pascal transitions, acronym-to-word boundaries,
and letter/digit boundaries; tokens are invariant-lowercase. Matching SHALL use
exact token equality only: no substring, glob, or regex matching. The default
role/name tokens are `dispatcher`, `registry`, `handler`, `loader`, `session`,
`options`, `configuration`, `command`, `diagnostic`, `mapper`, `dto`, `model`,
`service`, and `orchestrator`. `N_f` SHALL be `1.000000000` when at least one
token matches and `0.000000000` otherwise. All matched tokens SHALL be reported
in ascending ordinal order.

Using effective OCP weights `o_t,o_c,o_r,o_n`:

```text
OcpPressureScore(f) = Q(o_t*T_f + o_c*K_f + o_r*Q(normalized(E_f)) + o_n*N_f)
```

Bottleneck and OCP rankings SHALL be independent per primary-category cohort just
like hotspot rankings. Reports SHALL call these results `parallel-development
bottleneck` or `parallel-development pressure`, and `OCP pressure` or `likely
OCP violation`. They SHALL NOT claim that a merge conflict occurred or that an
OCP violation is proven unless separate direct evidence establishes that fact.

#### Scenario: Task participating in multiple pairs
- **WHEN** task `#101` forms independent pairs with both `#102` and `#103`
- **THEN** `Qualifying_f(#101)` is the SHA-deduplicated union of its pair-exclusive commit sets and each qualifying commit contributes at most once to `Repeated_f(#101)`

#### Scenario: Role hint tokenization
- **WHEN** canonical stems are `OrderService`, `DiagnosticMapper`, and `ViewModel`
- **THEN** tokenization yields exact role-token matches `service`, `diagnostic`/`mapper`, and `model` respectively, while a mere substring inside an unsplit token does not match

### Requirement: Stable rankings and refactoring investigations
Within one primary-category cohort, every ranked file finding SHALL use:

1. descending canonical score;
2. descending ordinary task spread;
3. descending churn;
4. descending commit count;
5. ascending canonical logical path.

Cross-category file findings SHALL remain grouped rather than interleaved by
score. Category groups SHALL use the canonical category order.

Within one endpoint-category cohort, co-change pairs SHALL rank by descending
canonical combined weight, descending canonical commit component, descending
canonical task component, then ascending first and second canonical logical
paths. Clusters SHALL rank within their endpoint-category cohort by descending
canonical maximum edge weight, descending canonical sum of member-edge weights,
then ascending first member canonical path. Cross-cohort pair and cluster results
SHALL remain grouped rather than interleaved by numeric weight.

Refactoring candidates SHALL be evidence-derived investigations, not automatic
redesign decisions. Each candidate SHALL carry its source finding identifiers,
component/evidence values, effective thresholds, category/cohort identity, and
interpretation caveat. Candidate threshold evaluation SHALL use canonical
quantized values from the finding's own cohort. Candidate collections SHALL be
grouped by finding family and cohort; there is no cross-category claim that a
higher normalized score represents greater absolute architecture pressure.

The canonical mapping is: high OCP pressure plus role-hint evidence suggests
investigating extension-point extraction; a high co-change cluster suggests
investigating a module or contract boundary; a high bottleneck suggests
investigating orchestration or feature separation; and a high test-only hotspot
suggests investigating fixture/helper architecture. When nothing qualifies, the
corresponding deterministic candidate collection is empty.

#### Scenario: Equivalent-score total order
- **WHEN** all numeric rank dimensions for two same-cohort file findings are equal
- **THEN** ascending canonical logical path provides the final deterministic discriminator

### Requirement: Deterministic report semantics and interpretation limits
The reporting layer SHALL produce stable markdown and canonical JSON from the
same file-level evidence. Markdown SHALL contain analyzed range and effective
configuration summary, production hotspots as the primary hotspot ranking,
separate non-production category rankings when present, co-change pair/cluster
cohort groups, bottlenecks, OCP pressure, refactoring candidates, and
limitations/interpretation notes.

Canonical JSON SHALL contain metadata/input refs, effective configuration
identity/summary, canonical numeric scale, primary path categories, canonical
paths and aliases, raw and canonical hotspot components, co-change cohort
identity and components, independent-task bottleneck evidence, OCP-pressure
evidence, effective score weights and thresholds, and refactoring candidates.

Canonical JSON arrays SHALL use the category/cohort grouping and stable ordering
defined by this capability and fields SHALL be serialized in the documented
schema order. Canonical derived real numbers SHALL use exactly nine fractional
digits without exponent notation. Generated timestamps and environment-specific
display values SHALL be excluded from canonical result identity. The Git-only
core SHALL remain independent of optional .NET/Roslyn enrichment; failed or
unavailable enrichment SHALL preserve the file-level finding unchanged.

The report SHALL state that churn is not complexity; high co-change is a
coupling/coordination signal rather than proof of module ownership; task and
author evidence may be incomplete; multi-reference commits are not independent
workstream proof; normalized scores are comparable only within their declared
category/cohort; role hints are bounded heuristics; and recommendations require
human review. Canonical scoring and recommendations SHALL not require LLM or
other stochastic inference.

#### Scenario: Deterministic report serialization
- **WHEN** identical canonical evidence is rendered twice
- **THEN** markdown ordering and canonical JSON content are identical without generated-time, checkout-root, locale, floating-point-format, or exponent-format differences

#### Scenario: Unavailable .NET enrichment
- **WHEN** optional C# enrichment cannot parse or map a changed file
- **THEN** the report retains the original file-level finding and records enrichment as unavailable rather than dropping or changing the score

### Requirement: Contributor theory reference
The repository SHALL contain a contributor-facing Release Architecture Forensics
reference that explains the product boundary, canonical input model, logical-file
identity, category-cohort normalization, nine-decimal canonical numeric model,
validated effective profiles, independent-task semantics, repeated-edit
aggregation, role-token matching, cluster threshold semantics, cohort-local
ranking, report semantics, configuration ownership, and interpretation limits in
terms consistent with this capability. The internal documentation index SHALL
link to that reference. It SHALL not be added to public MkDocs navigation until
an implementation change establishes a user-facing product contract.

#### Scenario: Contributor discovers theory
- **WHEN** a contributor opens the internal documentation index
- **THEN** it links to the Release Architecture Forensics theory reference
