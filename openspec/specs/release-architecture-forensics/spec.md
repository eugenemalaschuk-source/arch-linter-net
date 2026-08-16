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

Normalized author identity SHALL be trimmed invariant-lowercase email, else name,
else `unknown`. Task refs SHALL be deterministically extracted, deduplicated, and
ordered.

#### Scenario: Equivalent environments
- **WHEN** identical repository objects, refs, effective configuration, and tool version are analyzed from different checkout roots or timezones
- **THEN** canonical identity, evidence, rankings, and canonical JSON are identical

#### Scenario: Missing ref
- **WHEN** either explicit ref cannot be resolved unambiguously
- **THEN** analysis fails instead of selecting a default branch, checkout, or range

### Requirement: Canonical commit set and file-touch deltas
After refs resolve, the canonical analyzed commit set SHALL be:

```text
Commits(from,to) = Reachable(to) \ Reachable(from)
```

`Reachable(r)` includes commit `r` itself and all commits reachable from it by
following zero or more parent edges. This rule applies whether or not `from` is
an ancestor of `to`.

Canonical commit order SHALL be ascending by committer UTC epoch-second timestamp,
then ascending ordinal full commit SHA. All temporal metrics in this capability
SHALL use committer timestamps represented as UTC epoch seconds.

For a non-merge commit with exactly one parent, canonical file-touch evidence
SHALL be the tree delta from that parent to the commit. For a root commit with no
parent, the canonical parent tree SHALL be the empty Git tree.

For the initial deterministic profile, a merge commit with two or more parents
SHALL remain present in analyzed-range metadata but SHALL NOT contribute file
touches, churn, per-file commit count, per-file author spread, task-episode file
membership, rename evidence, co-change evidence, or any downstream file score.
The report SHALL expose the number of excluded merge commits and SHALL state that
merge-resolution-only edits may therefore be understated.

Only commits that contribute canonical file-touch evidence participate in
file-level temporal spans. Merge commit timestamps remain range metadata only.

#### Scenario: Reachability range
- **WHEN** a side-branch commit is reachable from `to` but not reachable from `from`
- **THEN** it belongs to the analyzed commit set regardless of enumeration or first-parent history

#### Scenario: Merge does not double-count
- **WHEN** two non-merge branch commits touch a file and a later merge commit joins those branches
- **THEN** the branch commits contribute file evidence while the merge remains metadata-only and does not add another file touch

#### Scenario: Root commit
- **WHEN** a root commit belongs to the analyzed commit set
- **THEN** its file evidence is computed against the empty tree

#### Scenario: Empty range
- **WHEN** `Reachable(to) \ Reachable(from)` contains no commits
- **THEN** analysis succeeds with deterministic empty/zero evidence

### Requirement: Canonical rename recognition and logical-file identity
For the initial deterministic profile, a rename relation SHALL be recognized
only inside one canonical non-merge commit delta as a one-to-one delete/add
relation whose deleted preimage and added postimage have exactly the same Git
blob object identity.

Similarity-based rename inference and copy inference SHALL NOT participate in
canonical logical-file identity in the initial profile. Ambient Git rename
thresholds, rename limits, client configuration, or backend heuristics SHALL NOT
change canonical evidence.

If one deleted source can correspond to multiple added destinations, one added
destination can correspond to multiple deleted sources, blob identity differs,
or the relationship is otherwise not one-to-one, the affected paths SHALL remain
separate logical identities.

A logical file SHALL represent one linear chain of these exact rename relations.
Its canonical path SHALL be the normalized repository-relative path at the last
in-range occurrence, including a deleted path when deletion is final.

Aliases SHALL contain each distinct historical non-canonical path exactly once.
Aliases SHALL be ordered by first in-range occurrence using canonical commit
order and then ascending ordinal path. The canonical path SHALL NOT also appear
in the alias collection.

Commit-file entries SHALL retain status, additions, deletions, observed path, and
logical-file identity. Churn SHALL be additions plus deletions summed over that
logical identity and SHALL be described as volume, not complexity.

A future similarity-based rename profile SHALL define its similarity algorithm,
threshold, candidate-limit behavior, copy semantics, compatibility identity, and
migration rules through reviewed specification work before it affects canonical
results.

#### Scenario: Exact rename across categories
- **WHEN** one commit exactly moves blob `src/Old.cs` to `tests/New.cs` with no competing source or destination
- **THEN** one logical file uses canonical path `tests/New.cs` and retains `src/Old.cs` as an alias

#### Scenario: Modified move is not an exact rename
- **WHEN** a delete/add pair has different blob object identities
- **THEN** the initial profile keeps the two paths as separate logical identities

#### Scenario: Split is not a rename chain
- **WHEN** one deleted blob has two same-blob added destinations in the same commit
- **THEN** no arbitrary destination is selected and the affected paths remain separate identities

#### Scenario: Alias de-duplication
- **WHEN** an exact rename chain moves `A` to `B` and later back to `A`
- **THEN** canonical path is `A`, aliases contain `B` exactly once, and canonical `A` is not duplicated as an alias

### Requirement: Path classification
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
- **WHEN** an exact logical-file chain ends at canonical path `tests/New.cs`
- **THEN** primary category is derived from `tests/New.cs`, not from an earlier alias

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

#237 ignore rules SHALL act as analysis filters before normalization and base-graph
construction. Presentation-only suppression SHALL NOT change canonical scores.
File-level populations SHALL contain retained logical files in the same primary
category. Edge-level populations SHALL contain base graph edges in the same
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
most nine fractional digits. Authored weight values SHALL use ordinary decimal
notation rather than exponent notation.

A scoring component SHALL be enabled if and only if its effective weight is
greater than zero. A zero effective weight SHALL mean disabled. Evidence
availability SHALL NOT change enabledness.

At least one component SHALL be enabled and each profile SHALL sum exactly to
`1.000000000` using the exact authored/effective decimal values. For co-change,
`alpha + beta = 1.000000000`. Validation SHALL occur before score quantization;
`Q(v)` SHALL NOT repair an invalid profile sum. Invalid profiles SHALL fail
validation instead of being silently rounded, rescaled, or renormalized.

#### Scenario: Invalid profile sum
- **WHEN** effective weights do not sum exactly to `1.000000000`
- **THEN** validation fails and analysis does not silently normalize them

#### Scenario: Evidence absence does not disable a component
- **WHEN** task evidence is absent but the effective task weight is positive
- **THEN** the component remains enabled with raw value zero and other weights remain unchanged

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

`temporal_span_seconds` SHALL be latest minus earliest canonical file-evidence
committer timestamp; a one-touch file has span zero. Findings SHALL retain raw
metrics, canonical components, and effective weights.

Hotspot rankings SHALL be independent per primary-category cohort. Production is
the default human-facing `top hotspots` ranking; non-production categories are
separate groups. Scores from different cohorts SHALL NOT be interleaved as one
numeric ranking.

#### Scenario: Cross-category hotspot scores
- **WHEN** docs score `0.950000000` and production score `0.800000000`
- **THEN** the report keeps them in separate category rankings rather than claiming docs outranks production

### Requirement: Canonical base co-change graph
After analysis ignores, the canonical base co-change graph SHALL be:

```text
G0 = (V, E0)
V  = retained logical files
E0 = { unordered (a,b) : a != b and CommitCoChange(a,b) > 0 }

CommitCoChange(a,b) = count(canonical file-evidence commits containing both a and b)
TaskCoChange(a,b)   = count(distinct tasks whose canonical file episodes contain both a and b)
```

Task co-change MAY contribute to the weight of an edge already in `E0` but SHALL
NOT create a base edge when `CommitCoChange(a,b) = 0` in the initial profile.

For each base edge:

```text
CommitComponent(a,b)  = Q(normalized(CommitCoChange(a,b)))
TaskComponent(a,b)    = Q(normalized(TaskCoChange(a,b)))
CombinedCoChange(a,b) = Q(alpha*CommitComponent + beta*TaskComponent)
```

Edge components SHALL normalize over `E0` edges inside their unordered
endpoint-category cohort. Pair paths SHALL be canonical paths in ascending
ordinal order. Pair rankings SHALL use `E0` and remain endpoint-cohort-local.

Distinct-neighbor degree, `IncidentCommitDegree`, `IncidentTaskDegree`, `IC_f`,
`IT_f`, and `K_f` SHALL always be derived from `G0`, never from a thresholded
cluster graph. The initial profile deliberately reuses the effective co-change
`alpha/beta` mix for `K_f`; it has no second hidden centrality mix.

#### Scenario: Task-only association does not create a base edge
- **WHEN** one task changes `A` in one commit and `B` in another commit but no canonical file-evidence commit changes them together
- **THEN** `TaskCoChange(A,B)` may be positive, `CommitCoChange(A,B)=0`, and `(A,B)` is not an edge in `E0`

#### Scenario: Co-change without tasks
- **WHEN** files change together but no task refs are extracted
- **THEN** a base edge exists from commit evidence, task evidence is zero, and effective weights remain unchanged

### Requirement: Threshold graph and deterministic clusters
A significance threshold SHALL be canonical, lie in
`[0.000000000,1.000000000]`, and apply only to canonical `CombinedCoChange`.
Qualification SHALL be inclusive.

When an effective threshold `theta` exists, define:

```text
Gtheta = (V, { e in E0 : CombinedCoChange(e) >= theta })
```

`Gtheta` SHALL be used only for cluster construction and cluster-derived candidate
logic. Changing `theta` SHALL NOT change `G0`, edge normalization populations,
pair weights/rankings, `D_f`, `IC_f`, `IT_f`, `K_f`, hotspot scores, bottleneck
scores, or OCP scores.

Clusters SHALL be connected components of `Gtheta` with at least two vertices,
constructed independently inside each endpoint-category cohort. With no effective
threshold, pair evidence remains and cluster output is empty.

For cluster `C` inside endpoint-category cohort `h`:

```text
ClusterEdges(C) =
  { e in Gtheta(h) : both endpoints of e are members of C }

ClusterMaximum(C) =
  max(CombinedCoChange(e) for e in ClusterEdges(C))

ClusterAggregate(C) =
  Q(sum(CombinedCoChange(e) for e in ClusterEdges(C)))
```

Sub-threshold `G0` edges between cluster members SHALL NOT participate in
`ClusterAggregate`. Clusters SHALL rank within their endpoint-category cohort by
descending `ClusterMaximum`, descending `ClusterAggregate`, then ascending first
canonical member path. Cluster members SHALL serialize in ascending canonical
path order.

#### Scenario: Threshold equality
- **WHEN** edge weight and threshold both equal `0.600000000`
- **THEN** the edge belongs to `Gtheta`

#### Scenario: Threshold does not rescore centrality
- **WHEN** the same `G0` is analyzed with no threshold and with a threshold that removes every edge from `Gtheta`
- **THEN** `D_f`, `IC_f`, `IT_f`, `K_f`, bottleneck scores, and OCP scores are identical while cluster output changes

#### Scenario: Cluster aggregate uses qualifying edges only
- **WHEN** `AB=0.600000000`, `BC=0.700000000`, `AC=0.590000000`, and `theta=0.600000000`
- **THEN** cluster `{A,B,C}` has maximum `0.700000000`, aggregate `1.300000000`, and `AC` does not contribute to the aggregate

### Requirement: Independent task evidence for parallel-development signals
A task episode SHALL be canonical file-evidence commits linked to one extracted
task ref. A commit MAY reference multiple tasks and contribute ordinary task
spread/task co-change to each, but that alone SHALL NOT establish independent
workstreams.

For file `f`, refs `x,y` form an independent pair only when both sides have
pair-exclusive evidence: at least one file-touching commit references `x` but not
`y`, and at least one references `y` but not `x`. Shared-reference commits SHALL
NOT establish independence or temporal overlap/proximity for that pair.

`IndependentTaskSpread(f)` SHALL count refs participating in at least one
independent pair. Each pair-side interval SHALL be the closed interval
`[min(committer_epoch_second), max(committer_epoch_second)]` over that side's
pair-exclusive canonical file-evidence commits.

```text
days_between = 0                              when closed intervals overlap
               ceil(positive UTC gap in days) otherwise
TemporalProximity(x,y) = Q(1/(1+days_between))
```

The file temporal value SHALL be the maximum canonical proximity across
independent pairs, or zero when none exists.

#### Scenario: One multi-reference commit
- **WHEN** the only file-touching commit references `#101` and `#102`
- **THEN** ordinary task spread may contain both refs but independent task spread, temporal proximity, and repeated-edit evidence are zero

#### Scenario: Shared commit does not collapse a gap
- **WHEN** pair-exclusive `#101` evidence and pair-exclusive `#102` evidence are separated by two days with a shared-reference commit between them
- **THEN** the shared commit does not enter either pair interval and proximity is derived only from the pair-exclusive intervals

### Requirement: Deterministic bottleneck centrality and score
Distinct-neighbor degree SHALL be counted from `G0` and normalized within the
file's primary-category cohort.

File centrality SHALL NOT sum endpoint-cohort-normalized edge scores. Instead,
for retained file `f` and its `G0` neighbors:

```text
IncidentCommitDegree(f) = Σ CommitCoChange(f,n)
IncidentTaskDegree(f)   = Σ TaskCoChange(f,n)
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
- **WHEN** a production file has both production-production and production-tests `G0` edges
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

### Requirement: Deterministic role-token evidence
Role hints SHALL operate on the canonical filename stem using this ASCII tokenizer:

1. any character outside `[A-Za-z0-9]` SHALL delimit tokens;
2. inside one ASCII alphanumeric run, split between a lowercase letter and a following uppercase letter;
3. split before the final uppercase letter of an uppercase run when the next character is lowercase;
4. split between a letter and a digit in either direction;
5. map ASCII `A-Z` to `a-z` by ordinal mapping.

Non-ASCII characters therefore act as delimiters in the initial profile.
Matching SHALL use exact token equality only; substring, glob, regex, and
culture-sensitive matching are forbidden.

Default role tokens are `dispatcher`, `registry`, `handler`, `loader`, `session`,
`options`, `configuration`, `command`, `diagnostic`, `mapper`, `dto`, `model`,
`service`, `orchestrator`. `N_f` SHALL be `1.000000000` when any token matches,
else zero. All matched role tokens SHALL report in ascending ordinal order.

```text
OcpPressureScore(f) = Q(o_t*T_f + o_c*K_f + o_r*Q(normalized(E_f)) + o_n*N_f)
```

OCP rankings SHALL be category-local. Reports SHALL describe bottleneck/OCP
results as heuristic pressure, never proof of a merge conflict or formal OCP
violation without separate direct evidence.

#### Scenario: Task in multiple independent pairs
- **WHEN** `#101` is independently paired with both `#102` and `#103`
- **THEN** its pair-exclusive sets are unioned and SHA-deduplicated before repeated-edit counting

#### Scenario: Role tokenizer vectors
- **WHEN** stems are `OrderService`, `DiagnosticMapper`, `ViewModel`, `XMLParser2`, `Serviceable`, and `MyDispatcherFactory`
- **THEN** relevant tokens are respectively `order/service`, `diagnostic/mapper`, `view/model`, `xml/parser/2`, `serviceable`, and `my/dispatcher/factory`; exact matches include `service`, `diagnostic`, `mapper`, `model`, and `dispatcher`, while `Serviceable` does not match `service`

### Requirement: Stable cohort-local rankings and refactoring investigations
Within one primary-category cohort, file findings SHALL rank by descending
canonical score, ordinary task spread, churn, commit count, then ascending
canonical path. Cross-category findings SHALL remain grouped in canonical
category order rather than interleaved by score.

Within one endpoint-category cohort, `G0` pairs SHALL rank by descending canonical
combined, commit, and task component, then paths. Clusters SHALL use the exact
`ClusterMaximum`/`ClusterAggregate` ordering above. Cross-cohort pair/cluster
results SHALL remain grouped.

Candidates SHALL carry source finding IDs, evidence/components, effective
thresholds, category/cohort identity, and caveats. Candidate threshold evaluation
SHALL use canonical values inside the finding's own cohort. Cluster-derived
candidate logic SHALL consume `Gtheta`; non-cluster file scores SHALL remain
`G0`-derived. High OCP plus role hint suggests extension-point investigation;
high co-change cluster suggests boundary investigation; high bottleneck suggests
orchestration/feature split; high test hotspot suggests fixture/helper work.
Empty qualifying sets SHALL remain empty.

#### Scenario: Equivalent-score total order
- **WHEN** same-cohort file findings tie on numeric rank dimensions
- **THEN** ascending canonical path is the final discriminator

### Requirement: Deterministic report semantics and canonical JSON
Markdown SHALL contain range/config summary, analyzed/excluded merge counts,
production hotspots, separate non-production rankings, co-change cohorts,
bottlenecks, OCP pressure, candidates, and interpretation limits.

Canonical JSON SHALL include input/config identity, commit-set/merge/rename
semantics identity, canonical numeric scale, paths/aliases, categories,
raw/canonical score components, effective weights/thresholds, `G0`/cluster cohort
identity, independent-task and centrality evidence, OCP evidence, excluded merge
count, and candidates.

Arrays SHALL preserve the category/cohort grouping and stable within-group
ordering defined by this capability. Object properties SHALL serialize in the
order declared by the versioned report schema owned by #243. Dynamic object/map
keys SHALL serialize in ascending ordinal key order after their canonical string
normalization.

Canonical JSON bytes SHALL use UTF-8 without a byte-order mark, LF (`\n`) line
endings, two-space indentation, no trailing whitespace, and exactly one terminal
LF. Canonical real values SHALL use exactly nine fractional digits without
exponent notation. The canonical report-artifact identity SHALL be over these
canonical JSON bytes, not an implementation's in-memory dictionary ordering.

Optional .NET/Roslyn enrichment SHALL remain downstream; failure SHALL NOT drop,
change, or reorder file-level findings.

Reports SHALL state that churn is not complexity; co-change is not module proof;
task/author evidence may be incomplete; multi-reference commits are not
independent-work proof; excluded merge file deltas can understate merge-resolution
edits; exact-blob rename recognition intentionally misses rename-with-edit cases;
normalized scores are comparable only in their declared category/cohort; role
hints are bounded heuristics; and people decide whether to refactor.

#### Scenario: Deterministic rendering
- **WHEN** identical canonical evidence is rendered by two conforming implementations
- **THEN** canonical JSON bytes are identical across environments

### Requirement: Contributor theory reference
The repository SHALL contain an internal contributor reference consistent with
this capability, including commit-set and merge semantics, exact rename
recognition, logical identity, category-cohort normalization, canonical numeric
rules, weight validation, `G0`/`Gtheta`, cluster aggregation, independent-task and
repeated-edit semantics, cohort-safe centrality, ASCII role tokenization,
cohort-local rankings, canonical JSON, reports, ownership, and interpretation
limits. The internal docs index SHALL link it; public MkDocs navigation SHALL not
advertise the feature before it ships.

#### Scenario: Contributor discovers theory
- **WHEN** a contributor opens the internal documentation index
- **THEN** it links to the Release Architecture Forensics theory reference
