# release-architecture-forensics Specification

## Purpose

Define deterministic Git-range evidence, scoring, findings, recommendations, and
report semantics for Release Architecture Forensics.

## Requirements

### Requirement: Explicit deterministic analysis identity
Release Architecture Forensics SHALL analyze an explicit Git range with
exclusive `from` and inclusive `to`. Both refs SHALL resolve before analysis;
missing, ambiguous, or unresolvable refs SHALL fail closed.

Canonical identity SHALL contain authored refs, resolved commit IDs, effective
`history_analysis` configuration identity, and tool version. It SHALL exclude
absolute checkout roots, generated timestamps, timezone, locale, and other
environment-dependent presentation data. Uncommitted working-tree state SHALL
not alter Git-only evidence.

Commits SHALL order ascending by committer UTC timestamp then ordinal SHA.
Normalized author identity SHALL be trimmed invariant-lowercase email, else name,
else `unknown`. Task refs SHALL be deterministically extracted, deduplicated, and
ordered.

#### Scenario: Equivalent environments
- **WHEN** identical repository objects, refs, effective configuration, and tool version are analyzed from different checkout roots or timezones
- **THEN** canonical identity, evidence, rankings, and JSON are identical

#### Scenario: Empty range
- **WHEN** an explicit range contains no commits
- **THEN** analysis succeeds with deterministic empty/zero evidence

### Requirement: Canonical logical-file identity and path classification
A logical file SHALL represent one unambiguous linear rename chain. Its canonical
path SHALL be the normalized repository-relative path at the last in-range
occurrence, including a deleted path when deletion is final. Earlier paths SHALL
remain ordered aliases and SHALL NOT receive independent scores. Copy, split,
merge, or otherwise ambiguous relationships SHALL remain separate identities.

Commit-file entries SHALL retain status, additions, deletions, observed path, and
logical-file identity. Churn SHALL be additions plus deletions summed over that
logical identity and SHALL be described as volume, not complexity.

Each logical file SHALL have one primary category derived from its canonical
path. Canonical category order SHALL be:

1. `production`
2. `tests`
3. `docs`
4. `generated`
5. `build_ci`
6. `samples_examples`
7. `unknown`

Alias classifications MAY remain evidence but SHALL NOT replace the primary
category or ranking path. #237 owns schema-backed bounded matching, ignores,
category overrides, thresholds, and effective configuration.

#### Scenario: Rename across categories
- **WHEN** `src/Old.cs` is unambiguously renamed to `tests/New.cs`
- **THEN** one logical file uses canonical path `tests/New.cs` and the category derived from that path

#### Scenario: Ambiguous rename evidence
- **WHEN** Git evidence represents a copy, split, merge, or ambiguous relationship
- **THEN** the analyzer keeps affected paths as separate logical identities

### Requirement: Total normalization, canonical numbers, and deterministic populations
For a non-negative population, mathematical normalization SHALL be:

```text
normalized(x) = 0                              when max(population) = 0
                x / max(population)            otherwise

normalized_log(x) = 0                          when max(population) = 0
                    log(1+x) / log(1+max)      otherwise
```

Empty/all-zero populations SHALL produce finite zero; missing optional evidence
SHALL contribute raw zero; runtime weight renormalization SHALL NOT occur.

Canonical derived real values SHALL use:

```text
Q(v) = round-half-to-even(v, 9 decimal places)
```

Every normalized component, temporal proximity, combined edge weight, final
score, and configured numeric threshold SHALL be reduced to `Q(v)` before it is
used for threshold comparison, ranking, or canonical serialization. The formulas
are mathematical definitions; implementations MAY use any higher-precision or
equivalent internal algorithm but SHALL emit the same correctly rounded result.
Canonical JSON SHALL serialize canonical reals with exactly nine fractional
digits, invariant culture, and no exponent notation. Raw commit/task/author/churn
counts and day gaps remain exact integers.

#237 ignore rules SHALL act as analysis filters before normalization/graph
construction. Presentation-only suppression SHALL NOT change canonical scores.
File-level populations SHALL contain retained logical files in the same primary
category. Edge-level populations SHALL contain retained edges in the same
unordered endpoint-category pair, ordered by canonical category order.

#### Scenario: Canonical numeric boundary
- **WHEN** two implementations use different internal floating-point or logarithm algorithms for the same mathematical input
- **THEN** they compare and serialize the same nine-decimal `Q(v)` value

#### Scenario: Category isolation
- **WHEN** generated churn is much larger than production churn
- **THEN** it does not set the production normalization maximum

### Requirement: Valid effective scoring configuration
Each run SHALL use one validated effective scoring configuration. Defaults are:
hotspot `(commit .30, churn .25, task .25, author .10, temporal .10)`;
bottleneck `(independent_task .35, author .15, temporal .20, degree .20,
centrality .10)`; OCP `(independent_task .40, centrality .25,
repeated_episode_edit .25, role_hint .10)`; co-change `(commit .75, task .25)`.

Every configured weight SHALL be a finite non-negative base-10 decimal with at
most nine fractional digits. Enabled components SHALL have positive weight,
disabled components weight zero, at least one component SHALL be enabled, and
each profile SHALL sum exactly to `1.000000000`. For co-change,
`alpha + beta = 1.000000000`. Invalid profiles SHALL fail validation instead of
being silently rescaled. Missing evidence SHALL NOT change effective weights.

#### Scenario: Invalid profile sum
- **WHEN** effective weights do not sum exactly to `1.000000000`
- **THEN** validation fails and analysis does not silently normalize them

### Requirement: Deterministic hotspot evidence
For retained file `f` within its primary-category population:

```text
C_f = Q(normalized(commit_count(f)))
H_f = Q(normalized_log(churn(f)))
T_f = Q(normalized(distinct_task_refs(f)))
A_f = Q(normalized(distinct_authors(f)))
R_f = Q(normalized(temporal_span_seconds(f)))

HotspotScore(f) = Q(w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f)
```

`temporal_span_seconds` SHALL be latest minus earliest UTC commit timestamp; a
one-commit file has span zero. Findings SHALL retain raw metrics, canonical
components, and effective weights.

Hotspot rankings SHALL be independent per primary-category cohort. Production is
the default human-facing `top hotspots` ranking; non-production categories are
separate groups. Scores from different cohorts SHALL NOT be interleaved as one
numeric ranking.

#### Scenario: Cross-category hotspot scores
- **WHEN** docs score `0.950000000` and production score `0.800000000`
- **THEN** the report keeps them in separate category rankings rather than claiming docs outranks production

### Requirement: Deterministic co-change evidence and clusters
For retained files `a,b`:

```text
CommitCoChange(a,b) = count(commits containing both a and b)
TaskCoChange(a,b)   = count(distinct tasks whose episodes contain both a and b)
CommitComponent(a,b) = Q(normalized(CommitCoChange))
TaskComponent(a,b)   = Q(normalized(TaskCoChange))
CombinedCoChange(a,b) = Q(alpha*CommitComponent + beta*TaskComponent)
```

Edge components SHALL normalize inside their unordered endpoint-category cohort.
Pair paths SHALL be canonical paths in ascending ordinal order.

A significance threshold SHALL be canonical, lie in `[0.000000000,1.000000000]`,
and apply only to canonical `CombinedCoChange`. Qualification SHALL be inclusive:
`CombinedCoChange >= threshold`. Raw counts and individual components SHALL NOT
be substituted for this comparison.

Clusters SHALL be connected components with at least two vertices, constructed
independently from qualifying edges inside each endpoint-category cohort. With no
effective threshold, pair evidence remains and cluster output is empty. Pair and
cluster rankings SHALL remain cohort-local.

#### Scenario: Threshold equality
- **WHEN** edge weight and threshold both equal `0.600000000`
- **THEN** the edge qualifies for cluster construction

#### Scenario: Co-change without tasks
- **WHEN** files co-change but no task refs are extracted
- **THEN** commit evidence remains, task evidence is zero, and effective weights remain unchanged

### Requirement: Independent task evidence for parallel-development signals
A task episode SHALL be commits linked to one extracted task ref. A commit MAY
reference multiple tasks and contribute ordinary task spread/task co-change to
each, but that alone SHALL NOT establish independent workstreams.

For file `f`, refs `x,y` form an independent pair only when both sides have
pair-exclusive evidence: at least one file-touching commit references `x` but not
`y`, and at least one references `y` but not `x`. Shared-reference commits SHALL
NOT establish independence or temporal overlap/proximity for that pair.

`IndependentTaskSpread(f)` SHALL count refs participating in at least one
independent pair. Pair-side intervals SHALL use pair-exclusive commits:

```text
days_between = 0                              when intervals overlap
               ceil(positive UTC gap in days) otherwise
TemporalProximity(x,y) = Q(1/(1+days_between))
```

The file temporal value SHALL be the maximum canonical proximity across
independent pairs, or zero when none exists.

#### Scenario: One multi-reference commit
- **WHEN** the only file-touching commit references `#101` and `#102`
- **THEN** ordinary task spread may contain both refs but independent task spread, temporal proximity, and repeated-edit evidence are zero

### Requirement: Deterministic bottleneck centrality and score
Distinct-neighbor degree SHALL be counted from the retained co-change graph and
normalized within the file's primary-category cohort.

File centrality SHALL NOT sum endpoint-cohort-normalized edge scores. Instead,
for retained file `f`:

```text
IncidentCommitDegree(f) = Σ CommitCoChange(f,n) over retained neighbors n
IncidentTaskDegree(f)   = Σ TaskCoChange(f,n) over retained neighbors n
IC_f = Q(normalized(IncidentCommitDegree(f))) within f's primary-category cohort
IT_f = Q(normalized(IncidentTaskDegree(f)))   within f's primary-category cohort
K_f  = Q(alpha*IC_f + beta*IT_f)
```

This gives one category-local centrality scale even when `f` has incident edges
from several endpoint-category cohorts.

Bottleneck components SHALL be:

```text
T_f = Q(normalized(IndependentTaskSpread(f)))
A_f = Q(normalized(author_spread(f)))
O_f = Q(normalized(independent_temporal_proximity(f)))
D_f = Q(normalized(distinct_neighbor_degree(f)))
K_f = canonical centrality above

BottleneckScore(f) = Q(b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f)
```

Bottleneck rankings SHALL be independent per primary-category cohort.

#### Scenario: Mixed-category incident edges
- **WHEN** a production file has both production-production and production-tests incident edges
- **THEN** its centrality uses raw incident commit/task degrees normalized in the production file cohort, not a sum of incomparable endpoint-cohort edge scores

### Requirement: Deterministic OCP-pressure evidence
OCP task spread SHALL use canonical normalized `IndependentTaskSpread(f)` and
centrality SHALL reuse the `K_f` definition above.

Repeated independent editing SHALL be total for tasks participating in several
independent pairs:

```text
Partners_f(t) = {u : (t,u) is independent for f}
PairExclusive_f(t,u) = {c touching f : c references t and not u}
Qualifying_f(t) = SHA-deduplicated union of PairExclusive_f(t,u) over Partners_f(t)
Repeated_f(t) = max(|Qualifying_f(t)| - 1, 0)
E_f = sum(Repeated_f(t) for t with Partners_f(t) non-empty)
```

A commit SHALL count at most once per task ref after SHA deduplication. It MAY
count once for two different task refs when it independently qualifies for each.
With no independent pair, `E_f=0`.

Role hints SHALL use the canonical filename without final extension, tokenized at
non-alphanumeric, camel/Pascal, acronym-to-word, and letter/digit boundaries,
then invariant-lowercased. Matching SHALL use exact token equality only. Default
tokens are `dispatcher`, `registry`, `handler`, `loader`, `session`, `options`,
`configuration`, `command`, `diagnostic`, `mapper`, `dto`, `model`, `service`,
`orchestrator`. `N_f` SHALL be `1.000000000` when any token matches, else zero;
all matched tokens SHALL be reported in ascending ordinal order.

```text
OcpPressureScore(f) = Q(o_t*T_f + o_c*K_f + o_r*Q(normalized(E_f)) + o_n*N_f)
```

OCP rankings SHALL be category-local. Reports SHALL describe bottleneck/OCP
results as heuristic pressure, never proof of a merge conflict or formal OCP
violation without separate direct evidence.

#### Scenario: Task in multiple independent pairs
- **WHEN** `#101` is independently paired with both `#102` and `#103`
- **THEN** its pair-exclusive sets are unioned and SHA-deduplicated before repeated-edit counting

#### Scenario: Role tokenization
- **WHEN** stems are `OrderService`, `DiagnosticMapper`, and `ViewModel`
- **THEN** exact matches are `service`, `diagnostic`/`mapper`, and `model`, while an embedded substring in an unsplit token does not match

### Requirement: Stable cohort-local rankings and refactoring investigations
Within one primary-category cohort, file findings SHALL rank by descending
canonical score, ordinary task spread, churn, commit count, then ascending
canonical path. Cross-category findings SHALL remain grouped in canonical
category order rather than interleaved by score.

Within one endpoint-category cohort, pairs SHALL rank by descending canonical
combined, commit, and task component, then paths. Clusters SHALL rank by
descending canonical maximum edge weight, canonical aggregate member-edge weight,
then first member path. Cross-cohort pair/cluster results SHALL remain grouped.

Candidates SHALL carry source finding IDs, evidence/components, effective
thresholds, category/cohort identity, and caveats. Candidate threshold evaluation
SHALL use canonical values inside the finding's own cohort. High OCP pressure plus
role hint suggests extension-point investigation; high co-change cluster suggests
boundary investigation; high bottleneck suggests orchestration/feature split;
high test-only hotspot suggests fixture/helper investigation. Empty qualifying
sets SHALL remain empty.

#### Scenario: Equivalent-score total order
- **WHEN** same-cohort file findings tie on numeric rank dimensions
- **THEN** ascending canonical path is the final discriminator

### Requirement: Deterministic report semantics and interpretation limits
Markdown SHALL contain range/config summary, production hotspot ranking, separate
non-production category rankings, co-change cohort groups, bottlenecks, OCP
pressure, candidates, and interpretation limits.

Canonical JSON SHALL include input/config identity, canonical numeric scale,
canonical paths/aliases, categories, raw/canonical score components, effective
weights/thresholds, co-change cohort identity, independent-task evidence,
centrality evidence, OCP evidence, and candidates. Arrays SHALL preserve the
category/cohort grouping and stable within-group ordering defined above.
Canonical reals SHALL use exactly nine fractional digits without exponent
notation.

Optional .NET/Roslyn enrichment SHALL remain downstream; failure SHALL NOT drop,
change, or reorder file-level findings. Reports SHALL state that churn is not
complexity, co-change is not module-ownership proof, task/author evidence may be
incomplete, multi-reference commits are not independent-work proof, normalized
scores are comparable only inside their declared category/cohort, role hints are
bounded heuristics, and refactoring requires human judgment.

#### Scenario: Deterministic rendering
- **WHEN** identical canonical evidence is rendered twice
- **THEN** markdown ordering and canonical JSON are identical across environments

### Requirement: Contributor theory reference
The repository SHALL contain an internal contributor reference consistent with
this capability, including logical identity, category-cohort normalization,
canonical numeric rules, weight validation, independent-task and repeated-edit
semantics, cohort-safe centrality, cluster thresholds, cohort-local rankings,
reports, ownership, and interpretation limits. The internal docs index SHALL link
it; public MkDocs navigation SHALL not advertise the feature before it ships.

#### Scenario: Contributor discovers theory
- **WHEN** a contributor opens the internal documentation index
- **THEN** it links to the Release Architecture Forensics theory reference
