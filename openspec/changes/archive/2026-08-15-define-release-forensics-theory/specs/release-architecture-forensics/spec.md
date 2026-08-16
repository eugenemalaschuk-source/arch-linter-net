## ADDED Requirements

### Requirement: Explicit deterministic analysis identity
Release Architecture Forensics SHALL analyze explicit exclusive-`from`,
inclusive-`to` refs that resolve before analysis. Canonical identity SHALL
contain authored/resolved refs, effective `history_analysis` config identity,
history-semantics profile identity, and tool version while excluding local
environment presentation data.

### Requirement: Canonical commit set and file evidence
The analyzed commit set SHALL be:

```text
Commits(from,to) = Reachable(to) \ Reachable(from)
```

Commits SHALL sort by committer UTC epoch second then full SHA. One-parent
commits SHALL derive file evidence from parent-tree → commit-tree delta; roots
SHALL compare to the empty tree. Merge commits SHALL remain range metadata but
SHALL NOT contribute file-derived evidence in the initial profile. Reports SHALL
expose excluded merge count and the merge-resolution-only limitation.

### Requirement: Canonical Git path text
Every Git path entering canonical evidence SHALL decode as strict UTF-8.
Ill-formed UTF-8 SHALL fail analysis closed. Locale/code-page fallback,
replacement decoding, and platform filesystem decoding SHALL NOT participate.

### Requirement: Canonical exact rename recognition
The initial profile SHALL recognize a rename only as a same-commit one-to-one
delete/add relation with identical Git blob identity. Similarity/copy inference
SHALL NOT affect canonical identity. One-to-many, many-to-one, rename-with-edit,
and otherwise ambiguous relations SHALL remain separate.

The last in-range path SHALL be canonical. Each distinct non-canonical historical
path SHALL appear once in aliases, ordered by first canonical occurrence then
ordinal path; canonical path SHALL NOT also appear as an alias.

### Requirement: Canonical file events, line counts, churn, and commit count
After logical identity construction, there SHALL be one canonical file event per
logical file per canonical file-evidence commit.

An exact-blob rename SHALL collapse its raw delete/add pair into one rename event
with old/new paths retained as evidence and:

```text
canonical_additions = 0
canonical_deletions = 0
canonical_churn     = 0
line_count_status   = exact_rename
```

A text event with meaningful line counts SHALL use those counts and status
`text`. A binary delta or any delta with unavailable meaningful line counts SHALL
use additions `0`, deletions `0`, status `binary_or_unavailable`, and SHALL NOT
substitute bytes, estimates, textconv, or backend sentinels.

`commit_count(f)` SHALL count distinct canonical file-evidence commits touching
logical file `f`, not raw delta entries. Churn SHALL sum canonical additions plus
deletions over canonical file events.

#### Scenario: Exact rename churn
- **WHEN** a 100-line file is moved by an exact-blob rename without content change
- **THEN** it records one file touch and zero additions, deletions, and churn

#### Scenario: Binary line counts
- **WHEN** meaningful line counts are unavailable for a binary delta
- **THEN** additions/deletions are zero and the limitation marker is retained

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
fractional digits and no exponent notation for canonical reals.

### Requirement: Valid effective weights
Weights SHALL be finite non-negative ordinary base-10 decimals with at most nine
fractional digits. A component SHALL be enabled iff effective weight is positive
and disabled iff zero. Evidence availability SHALL NOT change enabledness.

At least one component SHALL be enabled and each exact decimal profile SHALL sum
to `1.000000000`; co-change requires `alpha + beta = 1.000000000`. Validation
SHALL occur before `Q` and SHALL fail rather than repairing invalid sums.

### Requirement: Deterministic hotspot evidence
Hotspot components SHALL be canonicalized and combined with effective weights:

```text
C_f=Q(normalized(commit_count)); H_f=Q(normalized_log(churn))
T_f=Q(normalized(task_spread)); A_f=Q(normalized(author_spread))
R_f=Q(normalized(temporal_span))
HotspotScore=Q(w_c*C_f+w_h*H_f+w_t*T_f+w_a*A_f+w_r*R_f)
```

Rankings SHALL be category-local.

### Requirement: Canonical base co-change graph
The base graph SHALL be `G0=(V,E0)` where `V` is retained logical files and
`E0` contains exactly unordered pairs with `CommitCoChange>0`. Task co-change MAY
weight an existing base edge but SHALL NOT create one when commit co-change is
zero.

Base-edge commit/task components SHALL normalize inside their endpoint-category
cohort and combine as `Q(alpha*CommitComponent + beta*TaskComponent)`. Pair
rankings, distinct-neighbor degree, incident commit/task degree, and `K_f` SHALL
derive from `G0`.

### Requirement: Threshold graph and cluster aggregation
A configured significance threshold SHALL apply only to canonical
`CombinedCoChange` using inclusive `>=`:

```text
Gtheta = (V, {e in E0 : CombinedCoChange(e) >= theta})
```

`Gtheta` SHALL affect only clusters and cluster-derived candidates. It SHALL NOT
alter `G0`, normalization, pair ranking, `D_f`, `K_f`, hotspot, bottleneck, or OCP
scores.

For cluster `C`, `ClusterEdges` SHALL be qualifying `Gtheta` edges whose endpoints
belong to C, `ClusterMaximum` their maximum canonical combined weight, and
`ClusterAggregate` `Q` of their canonical combined-weight sum. Sub-threshold
internal `G0` edges SHALL NOT contribute.

### Requirement: Independent task evidence and temporal proximity
A multi-reference commit may contribute ordinary task spread/co-change but SHALL
not alone establish independent work. Two refs are independent for a file only
when each has at least one pair-exclusive canonical file-touch commit.

Pair intervals SHALL be closed intervals over committer epoch seconds. For
non-overlapping intervals:

```text
gap_seconds = later.start_epoch_second - earlier.end_epoch_second

days_between = 0                         when gap_seconds <= 0
               ceil(gap_seconds / 86400) when gap_seconds > 0

TemporalProximity = Q(1/(1+days_between))
```

Calendar dates, local midnight, timezone, and DST SHALL NOT participate.

### Requirement: Cohort-safe centrality and deterministic bottleneck score
File centrality SHALL NOT sum endpoint-cohort-normalized edge scores. Using `G0`:

```text
IncidentCommitDegree(f)=Σ CommitCoChange(f,n)
IncidentTaskDegree(f)=Σ TaskCoChange(f,n)
IC_f=Q(normalized(IncidentCommitDegree(f))) in f's file-category cohort
IT_f=Q(normalized(IncidentTaskDegree(f))) in f's file-category cohort
K_f=Q(alpha*IC_f+beta*IT_f)
```

Bottleneck score SHALL combine canonical independent-task spread, author spread,
temporal proximity, `G0` distinct-neighbor degree, and this `K_f`. Rankings SHALL
remain category-local.

### Requirement: Deterministic OCP evidence
OCP SHALL reuse independent-task spread and `G0`-derived `K_f`. Repeated editing
for task `t` SHALL union pair-exclusive commit sets across all independent
partners, deduplicate by SHA, then count qualifying commits after the first.
`E_f` SHALL sum those per-task repeated counts.

### Requirement: Portable role-token evidence
The initial tokenizer SHALL use ASCII rules only: non-`[A-Za-z0-9]` delimiters,
lowercase→uppercase split, acronym-final-uppercase-before-lowercase split,
letter↔digit split, then ordinal ASCII lowercase. Non-ASCII characters SHALL act
as delimiters. Matching SHALL use exact token equality only.

### Requirement: Stable cohort-local rankings and candidates
File, `G0` pair, cluster, and candidate results SHALL remain grouped by their
declared comparable cohort and rank only within that cohort. Cluster-derived
candidate logic SHALL consume `Gtheta`; non-cluster file scoring SHALL remain
`G0`-derived.

### Requirement: Canonical JSON string escaping and bytes
Canonical strings SHALL contain valid Unicode scalar values. Canonical JSON
escaping SHALL use `\"` for quote, `\\` for backslash, standard short escapes
for backspace/tab/newline/formfeed/carriage-return, `\u00XX` with uppercase hex
for other U+0000..U+001F controls, literal `/`, and direct UTF-8 for every other
Unicode scalar including non-ASCII.

Canonical JSON properties SHALL follow the versioned #243 schema order; dynamic
map keys SHALL use ascending ordinal order. Canonical bytes SHALL be UTF-8 without
BOM, LF line endings, two-space indentation, no trailing whitespace, exactly one
terminal LF, fixed nine-decimal canonical reals, no exponent notation, and the
escaping profile above. Report identity SHALL use those exact bytes.

Reports SHALL disclose excluded merge file evidence, strict UTF-8 path
requirements, exact-blob rename limits, exact-rename zero churn,
binary/unavailable zero churn with limitation markers, cohort-relative score
comparability, and other heuristic limitations.

#### Scenario: Canonical JSON escaping
- **WHEN** a canonical string contains quote, backslash, slash, control and non-ASCII scalar data
- **THEN** every conforming implementation emits identical canonical JSON bytes

### Requirement: Contributor reference
The internal contributor reference SHALL remain consistent with this capability,
including reachability/merge semantics, strict UTF-8 paths, canonical file-event
and churn rules, exact rename recognition, `G0/Gtheta`, cluster aggregation,
canonical numeric rules, exact temporal-gap semantics, cohort-safe centrality,
portable role tokenization, canonical JSON escaping/bytes, and interpretation
limits. Public MkDocs navigation SHALL not advertise the feature before
implementation ships.
