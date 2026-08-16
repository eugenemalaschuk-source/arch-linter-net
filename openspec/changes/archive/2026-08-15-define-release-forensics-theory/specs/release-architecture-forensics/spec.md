## ADDED Requirements

### Requirement: Explicit deterministic analysis identity
Release Architecture Forensics SHALL analyze explicit exclusive-`from`,
inclusive-`to` refs that resolve before analysis. Canonical identity SHALL
contain authored/resolved refs, effective `history_analysis` config identity, and
tool version while excluding checkout/environment presentation data. Authors and
task refs SHALL use deterministic normalization/order.

#### Scenario: Equivalent environments
- **WHEN** identical repository objects, refs, config, and tool version are analyzed in different environments
- **THEN** canonical evidence, rankings, and JSON are identical

### Requirement: Canonical commit set and file evidence
The analyzed commit set SHALL be `Reachable(to) \ Reachable(from)`, with
`Reachable(r)` including `r`. Commits SHALL sort by committer UTC epoch second,
then full SHA.

One-parent commits SHALL derive canonical file evidence from parent-tree →
commit-tree delta; roots SHALL compare to the empty tree. Merge commits SHALL
remain range metadata but SHALL NOT contribute file touches, churn, file author
spread, file task membership, rename/co-change evidence, or downstream file
scores in the initial profile. Reports SHALL expose excluded merge count and the
merge-resolution-only limitation.

#### Scenario: Merge does not double-count
- **WHEN** branch commits touch a file before a merge
- **THEN** branch commits contribute file evidence and the merge remains metadata-only for file scoring

### Requirement: Canonical exact rename recognition
The initial profile SHALL recognize a rename only as a same-commit one-to-one
delete/add relation with identical Git blob object identity. Similarity/copy
inference SHALL NOT affect canonical identity. One-to-many, many-to-one,
rename-with-edit, and otherwise ambiguous relations SHALL remain separate.

The last in-range path SHALL be canonical. Each distinct non-canonical historical
path SHALL appear once in aliases, ordered by first canonical commit occurrence
then ordinal path; the canonical path SHALL NOT also appear as an alias.

#### Scenario: Split is not a rename
- **WHEN** one deleted blob has two same-blob added destinations
- **THEN** no destination is chosen as a canonical rename and identities remain separate

### Requirement: Path categories and comparable cohorts
Primary category SHALL derive from canonical path in fixed order `production`,
`tests`, `docs`, `generated`, `build_ci`, `samples_examples`, `unknown`.
#237 ignores SHALL remove files before base graph/score populations. File metrics
normalize inside primary-category cohorts; base-edge metrics inside unordered
endpoint-category cohorts. Cross-cohort scores SHALL NOT be treated as globally
comparable.

### Requirement: Total normalization and canonical numbers
Mathematical normalization SHALL use zero for all-zero populations and otherwise
`x/max`, with logarithmic churn `log(1+x)/log(1+max)`. Missing evidence SHALL be
zero and weights SHALL NOT be implicitly renormalized.

Canonical reals SHALL use `Q(v) = round-half-to-even(v, 9 decimal places)` before
threshold comparison, ranking, or serialization. JSON SHALL emit exactly nine
fractional digits, invariant culture, no exponent notation.

#### Scenario: Numeric implementation variance
- **WHEN** two implementations use different internal math algorithms
- **THEN** they emit the same correctly rounded nine-decimal canonical values

### Requirement: Valid effective weights
Weights SHALL be finite non-negative ordinary base-10 decimals with at most nine
fractional digits; exponent notation SHALL NOT be canonical authoring. A
component SHALL be enabled iff effective weight is positive and disabled iff it
is zero. Evidence availability SHALL NOT change enabledness.

At least one component SHALL be enabled and each exact decimal profile SHALL sum
to `1.000000000`; co-change requires `alpha + beta = 1.000000000`. Validation
SHALL occur before `Q` and SHALL fail instead of repairing invalid sums.

#### Scenario: Invalid sum
- **WHEN** a profile does not sum exactly to `1.000000000`
- **THEN** validation fails rather than rounding or rescaling it

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

### Requirement: Canonical base co-change graph
The base graph SHALL be `G0=(V,E0)` where `V` is retained logical files and
`E0` contains exactly unordered pairs with `CommitCoChange>0`. Task co-change MAY
weight an existing base edge but SHALL NOT create one when commit co-change is
zero.

Base-edge commit/task components SHALL normalize inside their endpoint-category
cohort and combine as `Q(alpha*CommitComponent + beta*TaskComponent)`. Pair
rankings, distinct-neighbor degree, incident commit/task degree, and `K_f` SHALL
all derive from `G0`.

#### Scenario: Task-only association
- **WHEN** a task touches A and B in separate commits but no commit changes both
- **THEN** task co-change may be positive but no `G0` edge exists

### Requirement: Threshold graph and cluster aggregation
A configured significance threshold SHALL apply only to canonical
`CombinedCoChange` using inclusive `>=`.

```text
Gtheta = (V, {e in E0 : CombinedCoChange(e) >= theta})
```

`Gtheta` SHALL affect only clusters and cluster-derived candidates. It SHALL NOT
alter `G0`, normalization, pair ranking, `D_f`, `K_f`, hotspot, bottleneck, or OCP
scores. No threshold means no clusters.

For cluster `C`, `ClusterEdges` SHALL be qualifying `Gtheta` edges whose endpoints
belong to C, `ClusterMaximum` SHALL be their maximum canonical combined weight,
and `ClusterAggregate` SHALL be `Q` of their canonical combined-weight sum.
Sub-threshold internal `G0` edges SHALL NOT contribute. Cluster ranking SHALL use
maximum, aggregate, then first canonical member path.

#### Scenario: Cluster aggregate uses qualifying edges only
- **WHEN** AB=.600000000, BC=.700000000, AC=.590000000, theta=.600000000
- **THEN** cluster aggregate is `1.300000000` and AC is excluded

### Requirement: Independent task evidence
A multi-reference commit may contribute ordinary task spread/co-change but SHALL
not alone establish independent work. Two refs are independent for a file only
when each has at least one pair-exclusive canonical file-touch commit.

Pair intervals SHALL be closed `[min(committer_epoch_second),
max(committer_epoch_second)]` intervals. Shared commits SHALL NOT enter them.
Temporal proximity SHALL use canonical `Q(1/(1+days_between))`.

### Requirement: Cohort-safe centrality and deterministic bottleneck score
File centrality SHALL NOT sum endpoint-cohort-normalized edge scores. Using `G0`:

```text
IncidentCommitDegree(f)=Σ CommitCoChange(f,n)
IncidentTaskDegree(f)=Σ TaskCoChange(f,n)
IC_f=Q(normalized(IncidentCommitDegree(f))) in f's file-category cohort
IT_f=Q(normalized(IncidentTaskDegree(f))) in f's file-category cohort
K_f=Q(alpha*IC_f+beta*IT_f)
```

The initial centrality mix SHALL deliberately reuse effective co-change
`alpha/beta`. Bottleneck score SHALL combine canonical independent-task spread,
author spread, temporal proximity, `G0` distinct-neighbor degree, and this `K_f`.
Rankings remain category-local.

### Requirement: Deterministic OCP evidence
OCP SHALL reuse independent-task spread and `G0`-derived cohort-safe `K_f`.
Repeated editing for task `t` SHALL union pair-exclusive commit sets across all
independent partners, deduplicate by SHA, then count qualifying commits after the
first. `E_f` SHALL sum those per-task repeated counts.

### Requirement: Portable role-token evidence
The initial tokenizer SHALL use ASCII rules only: non-`[A-Za-z0-9]` delimiters,
lowercase→uppercase split, acronym-final-uppercase-before-lowercase split,
letter↔digit split, then ordinal ASCII lowercase. Non-ASCII characters SHALL act
as delimiters. Matching SHALL use exact token equality only.

#### Scenario: Exact role tokenization
- **WHEN** stems include `OrderService`, `XMLParser2`, and `Serviceable`
- **THEN** tokens include `order/service`, `xml/parser/2`, and `serviceable`; `Serviceable` does not match role token `service`

### Requirement: Stable cohort-local rankings and candidates
File, `G0` pair, cluster, and candidate results SHALL remain grouped by their
declared comparable cohort and rank only within that cohort. Cluster-derived
candidate logic SHALL consume `Gtheta`; non-cluster file scoring SHALL remain
`G0`-derived.

### Requirement: Deterministic canonical reports
Markdown/JSON SHALL preserve history-semantics identity, canonical numeric scale,
effective weights/thresholds, cohort grouping, centrality and independent-task
evidence, excluded merge count, and stable ordering.

Canonical JSON properties SHALL follow the versioned #243 report-schema order;
dynamic map keys SHALL use ascending ordinal key order. Canonical JSON bytes
SHALL be UTF-8 without BOM, LF line endings, two-space indentation, no trailing
whitespace, exactly one terminal LF, fixed nine-decimal canonical reals, and no
exponent notation. Report-artifact identity SHALL use those canonical bytes.

Reports SHALL disclose excluded merge file evidence, exact-blob rename limits,
cohort-relative score comparability, and other heuristic limitations.

#### Scenario: Deterministic rendering
- **WHEN** identical evidence is rendered twice
- **THEN** canonical JSON bytes are identical across environments

### Requirement: Contributor reference
The internal contributor reference SHALL remain consistent with this capability,
including reachability/merge semantics, exact rename recognition, `G0/Gtheta`,
cluster aggregation, canonical numeric rules, cohort-safe centrality, portable
role tokenization, canonical JSON, and interpretation limits. Public MkDocs
navigation SHALL not advertise the feature before implementation ships.
