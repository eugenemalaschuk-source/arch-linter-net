## ADDED Requirements

### Requirement: Explicit deterministic analysis identity
Release Architecture Forensics SHALL analyze an explicit Git range with
exclusive `from` and inclusive `to`. Both refs SHALL resolve before analysis;
missing, ambiguous, or unresolvable refs SHALL fail closed. Canonical identity
SHALL contain authored refs, resolved commit IDs, effective `history_analysis`
configuration identity, and tool version while excluding checkout roots,
generated timestamps, locale, timezone, and other environment presentation data.
Commit records SHALL sort by committer UTC timestamp then ordinal SHA. Authors
and task refs SHALL use deterministic normalization/order.

#### Scenario: Empty range
- **WHEN** an explicit range contains no commits
- **THEN** analysis succeeds with deterministic empty/zero evidence

### Requirement: Canonical logical-file identity and categories
A logical file SHALL represent one unambiguous linear rename chain. Its canonical
logical path SHALL be the normalized path at the last in-range occurrence,
including a deleted path when deletion is final. Earlier paths remain aliases and
SHALL NOT receive independent scores. Copy/split/merge/ambiguous relationships
remain separate identities.

Each logical file SHALL have one primary category from its canonical path:
`production`, `tests`, `docs`, `generated`, `build_ci`, `samples_examples`, or
`unknown`. Canonical category group order SHALL be exactly that order. Alias
classifications MAY remain evidence but do not replace the primary category.

#### Scenario: Rename across categories
- **WHEN** `src/Old.cs` is unambiguously renamed to `tests/New.cs`
- **THEN** one logical file is scored using canonical path `tests/New.cs` and its derived primary category

### Requirement: Total normalization, canonical numbers, and deterministic populations
The mathematical normalizers SHALL be:

```text
normalized(x) = 0                              when max(population) = 0
                x / max(population)            otherwise
normalized_log(x) = 0                          when max(population) = 0
                    log(1+x) / log(1+max)      otherwise
```

Empty/all-zero populations SHALL produce finite zero; missing evidence SHALL be
raw zero; runtime weight renormalization SHALL NOT occur. #237 ignore rules are
analysis filters applied before normalization/graph construction. File metrics
normalize inside the same primary-category cohort. Edge metrics normalize inside
the same unordered endpoint-category cohort.

Canonical derived real values SHALL use:

```text
Q(v) = round-half-to-even(v, 9 decimal places)
```

Every normalized component, temporal proximity, combined edge weight, final
score, and configured numeric threshold SHALL be reduced to `Q(v)` before
threshold comparison, ranking, or canonical serialization. Canonical JSON SHALL
emit exactly nine fractional digits, invariant culture, and no exponent notation.
Raw integral evidence remains integer data.

#### Scenario: Equivalent numeric implementations
- **WHEN** two implementations use different internal floating-point/log algorithms for the same mathematical input
- **THEN** they compare and serialize the same correctly rounded nine-decimal canonical value

### Requirement: Valid effective scoring configuration
Each score profile SHALL use finite non-negative base-10 weights with at most nine
fractional digits. Enabled components have positive weight, disabled components
weight zero, at least one component is enabled, and each profile sums exactly to
`1.000000000`. Invalid profiles SHALL fail validation rather than be rescaled.
For co-change, `alpha + beta = 1.000000000`.

Default profiles remain hotspot `(.30,.25,.25,.10,.10)`, bottleneck
`(.35,.15,.20,.20,.10)`, OCP `(.40,.25,.25,.10)`, and co-change `(.75,.25)`.

#### Scenario: Invalid profile sum
- **WHEN** effective weights do not sum exactly to `1.000000000`
- **THEN** validation fails instead of silently normalizing them

### Requirement: Deterministic hotspot evidence
Hotspot components SHALL be canonicalized and the final score SHALL consume the
effective profile:

```text
C_f = Q(normalized(commit_count(f)))
H_f = Q(normalized_log(churn(f)))
T_f = Q(normalized(distinct_task_refs(f)))
A_f = Q(normalized(distinct_authors(f)))
R_f = Q(normalized(temporal_span_seconds(f)))
HotspotScore(f) = Q(w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f)
```

Hotspot rankings SHALL be independent per primary-category cohort. Production is
the default human-facing `top hotspots` ranking; non-production groups are
separate. Scores from different cohorts SHALL NOT be interleaved as one numeric
ranking.

#### Scenario: Cross-category hotspot scores
- **WHEN** docs score `0.950000000` and production score `0.800000000`
- **THEN** the report keeps them in separate category rankings rather than claiming docs outranks production

### Requirement: Deterministic co-change and clusters
For retained files `a,b`:

```text
CommitCoChange = count(commits containing both)
TaskCoChange   = count(distinct tasks containing both)
CommitComponent = Q(normalized(CommitCoChange))
TaskComponent   = Q(normalized(TaskCoChange))
CombinedCoChange = Q(alpha*CommitComponent + beta*TaskComponent)
```

A significance threshold SHALL be a canonical value in `[0,1]` and SHALL apply
only to canonical `CombinedCoChange`; qualification is inclusive:
`CombinedCoChange >= threshold`. Clusters SHALL be connected components of
qualifying edges built independently inside each endpoint-category cohort. With
no threshold, cluster output is empty. Pair and cluster rankings are cohort-local.

#### Scenario: Threshold equality
- **WHEN** edge weight and threshold are both `0.600000000`
- **THEN** the edge qualifies for cluster construction

### Requirement: Independent task evidence
A multi-reference commit MAY contribute ordinary task spread/co-change but SHALL
NOT alone prove independent work. Refs `x,y` form an independent pair for file
`f` only if each has at least one file-touching commit referencing that ref but
not the other. Temporal intervals use pair-exclusive commits and:

```text
TemporalProximity = Q(1/(1+days_between))
```

`IndependentTaskSpread(f)` counts refs participating in at least one independent
pair.

#### Scenario: One multi-reference commit
- **WHEN** the only file-touching commit references `#101` and `#102`
- **THEN** independent task spread, temporal proximity, and repeated-edit evidence are zero

### Requirement: Deterministic bottleneck and OCP evidence
Bottleneck components SHALL use canonical normalized independent task spread,
author spread, temporal proximity, degree, and weighted degree:

```text
BottleneckScore = Q(b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f)
```

Repeated OCP editing SHALL use:

```text
Partners_f(t) = {u : (t,u) independent for f}
PairExclusive_f(t,u) = {c touching f : c references t and not u}
Qualifying_f(t) = SHA-deduplicated union of PairExclusive_f(t,u) over Partners_f(t)
Repeated_f(t) = max(|Qualifying_f(t)| - 1, 0)
E_f = sum(Repeated_f(t))
```

Thus a task participating in several independent pairs has one deterministic
SHA-deduplicated qualifying set and one commit counts at most once per task ref.
Role hints use deterministic stem tokenization and exact token equality only.

```text
OcpPressureScore = Q(o_t*T_f + o_c*K_f + o_r*Q(normalized(E_f)) + o_n*N_f)
```

Bottleneck/OCP rankings SHALL be category-local and remain heuristic evidence.

#### Scenario: Task in multiple independent pairs
- **WHEN** `#101` is independently paired with `#102` and `#103`
- **THEN** its pair-exclusive sets are unioned and SHA-deduplicated before repeated-edit counting

### Requirement: Stable cohort-local rankings and candidates
Within one file category, findings rank by descending canonical score, ordinary
task spread, churn, commit count, then canonical path. Within one endpoint
category pair, edges rank by combined/commit/task canonical weight then paths;
clusters rank by maximum edge, aggregate edge weight, then first member path.
Cross-cohort results SHALL remain grouped rather than interleaved by numeric score.

Candidates SHALL carry source evidence, effective thresholds, category/cohort
identity, and caveats. Candidate threshold comparison SHALL use canonical values
inside the finding's own cohort. Empty qualifying sets remain empty.

### Requirement: Deterministic report semantics
Markdown and canonical JSON SHALL preserve category/cohort grouping, canonical
numeric scale, stable ordering, effective weights/thresholds, independent-task
evidence, and interpretation limits. Canonical real numbers SHALL use exactly
nine fractional digits without exponent notation. Optional .NET enrichment is
downstream and SHALL NOT change/drop/reorder file-level findings.

Reports SHALL state that normalized scores are comparable only inside their
declared category/cohort, along with the existing churn/co-change/task/role-hint
limitations.

#### Scenario: Deterministic rendering
- **WHEN** identical canonical evidence is rendered twice
- **THEN** markdown ordering and canonical JSON content are identical across environments

### Requirement: Contributor theory reference
The repository SHALL contain an internal contributor reference consistent with
this capability, including logical identity, category-cohort normalization,
canonical numeric rules, weight validation, independent-task/repeated-edit
semantics, cluster threshold semantics, cohort-local rankings, reports, and
interpretation limits. Public MkDocs navigation SHALL not advertise the feature
before implementation ships.
