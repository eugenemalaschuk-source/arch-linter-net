## ADDED Requirements

### Requirement: Explicit deterministic analysis identity
Release Architecture Forensics SHALL analyze explicit exclusive-`from`,
inclusive-`to` refs that resolve before analysis. Canonical identity SHALL
contain authored/resolved refs, effective `history_analysis` config identity, and
tool version while excluding checkout/environment presentation data. Commits,
authors, and task refs SHALL use deterministic normalization/order.

#### Scenario: Equivalent environments
- **WHEN** identical repository objects, refs, config, and tool version are analyzed in different environments
- **THEN** canonical evidence, rankings, and JSON are identical

### Requirement: Canonical logical files and categories
One unambiguous linear rename chain SHALL be one logical file whose canonical
path is its last in-range occurrence. Earlier paths remain aliases. Ambiguous
copy/split/merge relationships remain separate identities. Primary category SHALL
come from the canonical path in fixed order: `production`, `tests`, `docs`,
`generated`, `build_ci`, `samples_examples`, `unknown`.

#### Scenario: Rename across categories
- **WHEN** `src/Old.cs` becomes `tests/New.cs`
- **THEN** one logical file uses `tests/New.cs` and its derived category

### Requirement: Total normalization and canonical numbers
Mathematical normalization SHALL use zero for all-zero populations and otherwise
`x/max`, with logarithmic churn `log(1+x)/log(1+max)`. Missing evidence SHALL be
zero and weights SHALL NOT be implicitly renormalized.

Canonical reals SHALL use `Q(v) = round-half-to-even(v, 9 decimal places)` before
threshold comparison, ranking, or serialization. JSON SHALL emit exactly nine
fractional digits, invariant culture, no exponent notation.

Ignored files SHALL be removed before graph/score populations. File metrics
normalize inside primary-category cohorts; edge metrics inside unordered
endpoint-category cohorts.

#### Scenario: Numeric implementation variance
- **WHEN** two implementations use different internal math algorithms
- **THEN** they emit the same correctly rounded nine-decimal canonical values

### Requirement: Valid effective weights
Weights SHALL be finite non-negative base-10 decimals with at most nine
fractional digits. Enabled components are positive, disabled components zero, at
least one is enabled, and each profile sums exactly to `1.000000000`. Co-change
therefore requires `alpha + beta = 1.000000000`. Invalid profiles fail validation.

#### Scenario: Invalid sum
- **WHEN** a profile does not sum to `1.000000000`
- **THEN** validation fails rather than rescaling it

### Requirement: Deterministic hotspot evidence
Hotspot components SHALL be canonicalized and combined with effective weights:

```text
C_f=Q(normalized(commit_count)); H_f=Q(normalized_log(churn))
T_f=Q(normalized(task_spread)); A_f=Q(normalized(author_spread))
R_f=Q(normalized(temporal_span))
HotspotScore=Q(w_c*C_f+w_h*H_f+w_t*T_f+w_a*A_f+w_r*R_f)
```

Rankings SHALL be category-local; production is the primary human ranking and
cross-category scores SHALL NOT be interleaved.

#### Scenario: Cross-category scores
- **WHEN** docs and production have different cohort-relative scores
- **THEN** they remain in separate rankings

### Requirement: Deterministic co-change and clusters
For each retained edge, raw commit/task co-change counts SHALL be normalized in
its endpoint-category cohort and combined as
`Q(alpha*CommitComponent + beta*TaskComponent)`. A configured significance
threshold SHALL apply only to canonical `CombinedCoChange` using inclusive `>=`.
Clusters SHALL be connected components of qualifying edges built independently
inside each endpoint-category cohort. No threshold means no clusters.

#### Scenario: Threshold equality
- **WHEN** edge and threshold are both `0.600000000`
- **THEN** the edge qualifies

### Requirement: Independent task evidence
A multi-reference commit may contribute ordinary task spread/co-change but SHALL
not alone establish independent work. Two refs are independent for a file only
when each has at least one pair-exclusive file-touching commit. Temporal
proximity uses pair-exclusive intervals and canonical `Q(1/(1+days_between))`.

#### Scenario: One multi-reference commit
- **WHEN** only one commit references both `#101` and `#102`
- **THEN** independent task spread, temporal proximity, and repeated-edit evidence are zero

### Requirement: Cohort-safe centrality and deterministic bottleneck score
File centrality SHALL NOT sum endpoint-cohort-normalized edge scores. Instead:

```text
IncidentCommitDegree(f)=Σ CommitCoChange(f,n)
IncidentTaskDegree(f)=Σ TaskCoChange(f,n)
IC_f=Q(normalized(IncidentCommitDegree(f))) in f's file-category cohort
IT_f=Q(normalized(IncidentTaskDegree(f))) in f's file-category cohort
K_f=Q(alpha*IC_f+beta*IT_f)
```

Bottleneck score SHALL combine canonical independent-task spread, author spread,
temporal proximity, distinct-neighbor degree, and this `K_f` with effective
weights. Rankings remain category-local.

#### Scenario: Mixed-category incident edges
- **WHEN** a file has incident edges from multiple endpoint-category cohorts
- **THEN** centrality uses raw incident counts normalized in the file cohort rather than summing incomparable edge scores

### Requirement: Deterministic OCP evidence
OCP SHALL reuse independent-task spread and cohort-safe `K_f`. Repeated editing
for task `t` SHALL union pair-exclusive commit sets across all independent
partners, deduplicate by SHA, then count qualifying commits after the first.
`E_f` SHALL sum those per-task repeated counts. Role hints SHALL use deterministic
identifier tokenization and exact token equality. Final OCP score SHALL be
canonicalized with `Q`.

#### Scenario: Task in several independent pairs
- **WHEN** `#101` is independent from `#102` and `#103`
- **THEN** its qualifying sets are unioned and SHA-deduplicated before counting repeated edits

### Requirement: Stable cohort-local rankings and candidates
File, edge, cluster, and candidate results SHALL remain grouped by their declared
category/cohort and rank only within that comparable cohort. Candidates SHALL
carry source evidence, effective thresholds, category/cohort identity, and
caveats; empty qualifying sets stay empty.

#### Scenario: Same-cohort tie
- **WHEN** two same-cohort findings tie numerically
- **THEN** canonical path is the final deterministic discriminator

### Requirement: Deterministic reports
Markdown/JSON SHALL preserve canonical numeric scale, effective weights and
thresholds, category/cohort grouping, centrality and independent-task evidence,
and stable ordering. Optional .NET enrichment SHALL remain downstream and SHALL
not alter file-level findings. Reports SHALL explain that normalized scores are
comparable only inside their declared cohort.

#### Scenario: Deterministic rendering
- **WHEN** identical evidence is rendered twice
- **THEN** canonical output is identical across environments

### Requirement: Contributor reference
The internal contributor reference SHALL remain consistent with this capability,
including canonical numeric rules, cohort-local normalization/ranking,
independent-task semantics, cohort-safe centrality, cluster thresholds, and
interpretation limits. Public MkDocs navigation SHALL not advertise the feature
before implementation ships.

#### Scenario: Contributor discovers theory
- **WHEN** the internal docs index is opened
- **THEN** it links to the Release Architecture Forensics theory reference
