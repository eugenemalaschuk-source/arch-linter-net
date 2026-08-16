# release-architecture-forensics Specification

## Purpose

Define the deterministic Git-range evidence, scoring, findings, recommendations,
and canonical report semantics for Release Architecture Forensics.

## Requirements

### Requirement: Explicit deterministic analysis identity
Release Architecture Forensics SHALL analyze an explicit Git range with
exclusive `from` and inclusive `to`. Both refs SHALL resolve before analysis;
missing, ambiguous, or unresolvable refs SHALL fail closed.

Canonical identity SHALL contain authored refs, resolved commit IDs, effective
`history_analysis` configuration identity, history-semantics profile identity,
and tool version. It SHALL exclude absolute checkout roots, generated timestamps,
timezone, locale, process state, and other environment-dependent presentation
data. Uncommitted working-tree state SHALL NOT alter Git-only evidence.

Normalized author identity SHALL be trimmed invariant-lowercase email when
present, otherwise trimmed invariant-lowercase author name, otherwise `unknown`.
Task refs SHALL be deterministically extracted, deduplicated, and ordered.

#### Scenario: Equivalent environments
- **WHEN** identical repository objects, refs, effective configuration, history-semantics profile, and tool version are analyzed in different environments
- **THEN** canonical identity, evidence, rankings, and canonical JSON bytes are identical

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
then ascending ordinal full commit SHA. All temporal metrics SHALL use committer
timestamps represented as UTC epoch seconds.

For a non-merge commit with exactly one parent, canonical file-touch evidence
SHALL come from the tree delta from that parent to the commit. For a root commit,
the canonical parent tree SHALL be the empty Git tree.

For the initial deterministic profile, a merge commit with two or more parents
SHALL remain present in analyzed-range metadata but SHALL NOT contribute file
touches, churn, per-file commit count, per-file author spread, task-episode file
membership, rename evidence, co-change evidence, or any downstream file score.
Reports SHALL expose the excluded merge count and state that merge-resolution-only
edits may therefore be understated.

Only commits contributing canonical file-touch evidence participate in file-level
temporal spans. Merge timestamps remain range metadata only.

#### Scenario: Reachability range
- **WHEN** a side-branch commit is reachable from `to` but not from `from`
- **THEN** it belongs to the analyzed commit set regardless of traversal or first-parent enumeration

#### Scenario: Merge does not double-count
- **WHEN** two non-merge branch commits touch a file and a later merge joins those branches
- **THEN** the branch commits contribute file evidence while the merge remains metadata-only

#### Scenario: Root commit
- **WHEN** a root commit belongs to the analyzed range
- **THEN** its file delta is computed against the empty tree

#### Scenario: Empty range
- **WHEN** `Reachable(to) \ Reachable(from)` is empty
- **THEN** analysis succeeds with deterministic empty/zero evidence

### Requirement: Canonical Git path text and string ordering
Git tree paths are byte sequences. Every path participating in canonical evidence
in the initial profile SHALL decode as strict UTF-8. Invalid, overlong, truncated,
or otherwise ill-formed UTF-8 SHALL fail analysis closed before classification,
rename chaining, ranking, or JSON serialization.

Implementations SHALL NOT use locale/code-page fallback, replacement characters,
or platform filesystem decoding. Strict UTF-8 decoding SHALL preserve the exact
Unicode scalar sequence encoded by the Git path bytes. Unicode normalization
(NFC, NFD, NFKC, NFKD) SHALL NOT be applied.

Canonical string ordering for paths, aliases, dynamic-map keys, and any other
field specified as ordinal SHALL be lexicographic by Unicode scalar numeric value.
At the first differing scalar, the lower scalar value sorts first; when one scalar
sequence is an exact prefix of the other, the shorter sequence sorts first.
This definition SHALL NOT depend on UTF-16 code-unit order, UTF-32 representation,
locale collation, filesystem collation, or Unicode normalization libraries.
Repository-relative `/` separators are canonical.

#### Scenario: Non-UTF8 Git path
- **WHEN** a path's raw Git bytes are not valid UTF-8
- **THEN** analysis fails instead of locale-decoding or inserting replacement characters

#### Scenario: Canonically distinct Unicode spellings
- **WHEN** two valid UTF-8 paths encode canonically equivalent but scalar-distinct Unicode sequences such as precomposed and decomposed accents
- **THEN** they remain distinct paths and sort by their actual decoded scalar sequences without Unicode normalization

#### Scenario: Supplementary scalar ordering
- **WHEN** canonical strings contain valid non-BMP Unicode scalars
- **THEN** ordering follows scalar numeric values rather than host-language code-unit ordering

### Requirement: Canonical exact rename recognition and logical-file identity
A rename SHALL be recognized only inside one canonical non-merge commit delta as
a one-to-one delete/add relation whose deleted preimage and added postimage have
exactly the same Git blob object identity.

Similarity-based rename inference and copy inference SHALL NOT participate in
canonical logical-file identity in the initial profile. Ambient Git rename
thresholds, candidate limits, client configuration, or backend heuristics SHALL
NOT change canonical evidence.

If one deleted source can correspond to multiple added destinations, one added
destination can correspond to multiple deleted sources, blob identity differs,
or the relationship is otherwise not one-to-one, affected paths SHALL remain
separate logical identities.

A logical file SHALL represent one linear chain of exact rename relations. Its
canonical path SHALL be the last in-range occurrence, including the deleted path
when deletion is final.

Aliases SHALL contain each distinct historical non-canonical path exactly once.
Aliases SHALL be ordered by first in-range occurrence using canonical commit
order and then canonical scalar-value string ordering. The canonical path SHALL
NOT also appear in aliases.

#### Scenario: Exact rename across categories
- **WHEN** one commit exactly moves blob `src/Old.cs` to `tests/New.cs` with no competing source or destination
- **THEN** one logical file uses `tests/New.cs` as canonical path and retains `src/Old.cs` as an alias

#### Scenario: Modified move is not an exact rename
- **WHEN** a delete/add pair has different blob object identities
- **THEN** the initial profile keeps both paths as separate logical identities

#### Scenario: Split is not a rename chain
- **WHEN** one deleted blob has two same-blob added destinations in one commit
- **THEN** no arbitrary destination is selected and all affected paths remain separate identities

#### Scenario: Alias de-duplication
- **WHEN** an exact rename chain moves `A` to `B` and later back to `A`
- **THEN** canonical path is `A`, aliases contain `B` once, and canonical `A` is not duplicated as an alias

### Requirement: Canonical file events, binary classification, and line churn
Canonical file evidence SHALL contain one logical-file event per logical file per
canonical file-evidence commit.

After exact rename recognition, the matching delete/add raw entries SHALL collapse
into one `rename` event retaining old path, new path, blob identity, and rename
status. Its content counts SHALL be:

```text
canonical_additions = 0
canonical_deletions = 0
canonical_churn     = 0
line_count_status   = exact_rename
```

A pure exact-blob move therefore records one file touch but zero content churn.

For every other file event, required old/new Git object content SHALL be loaded
from repository objects. If an object required by the analyzed refs is missing or
unreadable, analysis SHALL fail closed rather than invent zero evidence.

A participating side that is absent because the event is an add or delete SHALL
be represented by the empty byte sequence. Gitlink/tree/non-blob entries, or an
event for which line semantics are structurally not applicable, SHALL have:

```text
canonical_additions = 0
canonical_deletions = 0
line_count_status   = binary_or_unavailable
```

For blob-to-blob, empty-to-blob, or blob-to-empty events, if either non-empty blob
contains byte `0x00`, the event SHALL also use `binary_or_unavailable` and zero
canonical additions/deletions. Implementations SHALL NOT substitute byte counts,
estimated lines, textconv output, external-diff output, or backend sentinels.

Otherwise the event SHALL be canonical text evidence computed over raw blob bytes,
without decoding file contents to Unicode:

1. `Lines(bytes)` splits on byte `0x0A` (LF).
2. LF terminates a line and is not part of the line payload.
3. `0x0D` (CR) and every other byte remain part of the line payload.
4. Empty byte sequence has zero lines.
5. A final non-empty or empty segment after the last LF is a line only when bytes remain after that LF; a terminal LF does not create an additional trailing line.
6. Line equality is exact byte-sequence equality.
7. Let `L` be the mathematical length of a longest common subsequence of the old and new line sequences.

Canonical counts SHALL be:

```text
canonical_deletions = old_line_count - L
canonical_additions = new_line_count - L
line_count_status   = text
```

The LCS *length* is unique even when multiple LCS alignments exist, so these
addition/deletion totals SHALL NOT depend on diff-algorithm tie-breaking, Git diff
heuristics, attributes, textconv, external diff, or backend implementation.

If multiple raw entries from one commit map to the same logical file after
canonical identity construction, the analyzer SHALL emit one canonical file
commit touch and aggregate canonical content counts only after exact-rename
collapse.

`commit_count(f)` SHALL equal the number of distinct canonical file-evidence
commits touching logical file `f`, not raw delta-entry count.

```text
churn(f) = Σ(canonical_additions(event) + canonical_deletions(event))
           over canonical file events for f
```

Churn is change volume, not complexity. Exact-rename zero churn and
binary/unavailable zero line churn are deliberate v1 limitations and SHALL be
visible in report interpretation notes.

#### Scenario: Pure exact rename has zero churn
- **WHEN** a 100-line file moves by exact-blob rename with no content change
- **THEN** the logical file receives one commit touch and zero additions, deletions, and churn

#### Scenario: Deterministic text line counts
- **WHEN** two text blobs can be aligned by several equally valid diff scripts
- **THEN** additions/deletions derive only from old/new line counts and mathematical LCS length, producing identical totals across implementations

#### Scenario: NUL-containing blob
- **WHEN** either non-empty participating blob contains byte `0x00`
- **THEN** additions/deletions are zero and status is `binary_or_unavailable`

#### Scenario: Missing required blob object
- **WHEN** a required blob object cannot be loaded from the repository object database
- **THEN** analysis fails closed instead of treating the content as zero churn

#### Scenario: Commit count is commit-distinct
- **WHEN** raw normalization yields several entries for the same logical file in one canonical file-evidence commit
- **THEN** `commit_count` increases by exactly one

### Requirement: Path classification
Each logical file SHALL have one primary category derived from its canonical path.
Canonical category order SHALL be:

1. `production`
2. `tests`
3. `docs`
4. `generated`
5. `build_ci`
6. `samples_examples`
7. `unknown`

Alias classifications MAY remain evidence but SHALL NOT replace primary category
or ranking path. #237 owns schema-backed bounded matching, ignores, category
overrides, thresholds, and effective configuration. `unknown` remains visible.

#### Scenario: Rename across categories
- **WHEN** an exact logical-file chain ends at `tests/New.cs`
- **THEN** primary category derives from `tests/New.cs`, not an earlier alias

### Requirement: Total normalization, canonical numbers, and populations
For any non-negative population:

```text
normalized(x) = 0                              when max(population) = 0
                x / max(population)            otherwise

normalized_log(x) = 0                          when max(population) = 0
                    log(1+x) / log(1+max)      otherwise
```

Empty/all-zero populations SHALL produce finite zero. Missing optional evidence
SHALL contribute raw zero. Runtime weight renormalization SHALL NOT occur.

Canonical derived real values SHALL use:

```text
Q(v) = round-half-to-even(v, 9 decimal places)
```

Every normalized component, temporal proximity, combined edge weight, final
score, and numeric threshold SHALL be reduced to `Q(v)` before threshold
comparison, ranking, or canonical serialization. Mathematical formulas are
authoritative; implementations MAY use any internal algorithm that produces the
same correctly rounded result.

#237 analysis ignores SHALL remove logical files before score populations and
`G0` construction. Presentation-only suppression SHALL NOT change canonical
scores. File-level populations contain retained logical files in the same primary
category. Edge populations contain `G0` edges in the same unordered endpoint-
category cohort.

#### Scenario: Category isolation
- **WHEN** generated churn is much larger than production churn
- **THEN** it does not set the production normalization maximum

#### Scenario: All-zero population
- **WHEN** every raw value in a component population is zero
- **THEN** every normalized component is canonical zero rather than NaN/Infinity

### Requirement: Valid effective scoring configuration
Each run SHALL have one validated effective scoring configuration. Initial
default profiles are:

- hotspot: commit `.30`, churn `.25`, task `.25`, author `.10`, temporal `.10`;
- bottleneck: independent task `.35`, author `.15`, temporal `.20`, degree `.20`, centrality `.10`;
- OCP: independent task `.40`, centrality `.25`, repeated edit `.25`, role hint `.10`;
- co-change: commit `.75`, task `.25`.

Each configured weight SHALL be a finite non-negative ordinary base-10 decimal
with at most nine fractional digits. Exponent-form authoring is not canonical.
Positive weight means enabled; zero means disabled; at least one component SHALL
be enabled; every exact profile SHALL sum to `1.000000000`. Co-change therefore
requires `alpha + beta = 1.000000000`.

Validation SHALL occur before `Q`; invalid profiles SHALL fail instead of being
rounded, rescaled, or normalized. Evidence absence SHALL NOT change weights or
enabledness.

#### Scenario: Invalid profile sum
- **WHEN** effective weights do not sum exactly to `1.000000000`
- **THEN** validation fails and analysis does not repair the profile

#### Scenario: Missing task evidence
- **WHEN** task evidence is absent but task weight is positive
- **THEN** the task component remains enabled with raw zero and other weights remain unchanged

### Requirement: Deterministic hotspot evidence
For retained file `f`, using its primary-category population:

```text
C_f = Q(normalized(commit_count(f)))
H_f = Q(normalized_log(churn(f)))
T_f = Q(normalized(distinct_task_refs(f)))
A_f = Q(normalized(distinct_authors(f)))
R_f = Q(normalized(temporal_span_seconds(f)))

HotspotScore(f) = Q(w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f)
```

`temporal_span_seconds` is latest minus earliest canonical file-evidence committer
epoch second; a one-touch file has span zero. Findings retain raw metrics,
canonical components, effective weights, primary category, and line-count status
needed to interpret churn.

Hotspot rankings SHALL be independent per primary category. Production is the
default human-facing top-hotspot group. Cross-category scores SHALL NOT be
interleaved as one numeric ranking.

#### Scenario: Cross-category scores
- **WHEN** docs hotspot score is `0.950000000` and production score is `0.800000000`
- **THEN** the report does not claim the docs file outranks the production file

### Requirement: Canonical base co-change graph
After analysis ignores:

```text
G0 = (V,E0)
V  = retained logical files
E0 = { unordered(a,b) : a != b and CommitCoChange(a,b) > 0 }

CommitCoChange(a,b) = count(canonical file-evidence commits containing both)
TaskCoChange(a,b)   = count(distinct tasks whose canonical file episodes contain both)
```

Task co-change MAY weight an existing `G0` edge but SHALL NOT create an edge when
`CommitCoChange=0`.

For each `G0` edge:

```text
CommitComponent  = Q(normalized(CommitCoChange))
TaskComponent    = Q(normalized(TaskCoChange))
CombinedCoChange = Q(alpha*CommitComponent + beta*TaskComponent)
```

Edge components SHALL normalize within the edge's unordered endpoint-category
cohort. Pair paths SHALL use canonical scalar-value string ordering. Pair
rankings are endpoint-cohort-local.

Distinct-neighbor degree, incident degrees, `IC_f`, `IT_f`, and `K_f` SHALL always
use `G0`, never `Gtheta`. V1 deliberately reuses effective co-change
`alpha/beta` for centrality; there is no second hidden mix.

#### Scenario: Task-only association
- **WHEN** one task changes A in one commit and B in another but no commit changes both
- **THEN** TaskCoChange may be positive, CommitCoChange is zero, and no `G0` edge exists

#### Scenario: Co-change without tasks
- **WHEN** files change together but no task refs exist
- **THEN** a `G0` edge exists from commit evidence while task evidence is zero and weights remain unchanged

### Requirement: Threshold graph and deterministic clusters
A configured significance threshold SHALL be canonical, lie in `[0,1]`, apply
only to canonical `CombinedCoChange`, and use inclusive `>=`.

```text
Gtheta = (V, { e in E0 : CombinedCoChange(e) >= theta })
```

`Gtheta` SHALL be used only for cluster construction and cluster-derived
candidate logic. Changing `theta` SHALL NOT change `G0`, edge populations, pair
weights/ranking, `D_f`, `IC_f`, `IT_f`, `K_f`, hotspot, bottleneck, or OCP scores.

Clusters are connected components of `Gtheta` with at least two vertices,
constructed independently inside endpoint-category cohorts. Without a threshold,
pair evidence remains and cluster output is empty.

For cluster `C`:

```text
ClusterEdges(C) = { qualifying Gtheta edges whose endpoints are members of C }
ClusterMaximum(C) = max(CombinedCoChange(e) for e in ClusterEdges(C))
ClusterAggregate(C) = Q(sum(CombinedCoChange(e) for e in ClusterEdges(C)))
```

Sub-threshold internal `G0` edges SHALL NOT contribute. Cluster members serialize
in canonical scalar-value path order. Cluster ranking is descending maximum,
descending aggregate, then ascending first member path.

#### Scenario: Threshold equality
- **WHEN** edge weight equals the threshold
- **THEN** the edge qualifies for `Gtheta`

#### Scenario: Threshold does not rescore files
- **WHEN** threshold changes remove all `Gtheta` edges while `G0` is unchanged
- **THEN** `D_f`, `K_f`, bottleneck, and OCP scores remain identical

#### Scenario: Qualifying-edge aggregate
- **WHEN** AB=.600000000, BC=.700000000, AC=.590000000, theta=.600000000
- **THEN** cluster `{A,B,C}` has maximum `.700000000`, aggregate `1.300000000`, and AC contributes nothing

### Requirement: Independent task evidence and temporal proximity
A task episode is canonical file-evidence commits linked to one extracted task
ref. A multi-reference commit MAY contribute ordinary task breadth and task
co-change but SHALL NOT alone establish independent work.

For file `f`, refs `x,y` form an independent pair only when each side has at
least one pair-exclusive canonical file-evidence commit touching `f`. Shared-ref
commits do not establish independence and do not enter pair-exclusive intervals.

`IndependentTaskSpread(f)` counts task refs participating in at least one
independent pair.

Each pair-side interval is the closed interval
`[min(committer_epoch_second), max(committer_epoch_second)]` of its pair-exclusive
commits. For two intervals, identify earlier and later and define:

```text
gap_seconds = later.start_epoch_second - earlier.end_epoch_second

days_between = 0                         when gap_seconds <= 0
               ceil(gap_seconds / 86400) when gap_seconds > 0

TemporalProximity(x,y) = Q(1 / (1 + days_between))
```

For positive integer gaps, `(gap_seconds + 86399) div 86400` is equivalent.
Calendar dates, local midnight, timezone, DST, and fractional-day truncation SHALL
NOT participate. File temporal value is maximum canonical pair proximity, or zero
when no independent pair exists.

#### Scenario: One multi-reference commit
- **WHEN** the only file-touch commit references `#101` and `#102`
- **THEN** ordinary task breadth may contain both refs but independent spread and temporal proximity are zero

#### Scenario: Twenty-five hour gap
- **WHEN** pair-exclusive intervals have a positive gap of 90000 seconds
- **THEN** `days_between=2` and proximity is `0.333333333`

### Requirement: Cohort-safe bottleneck centrality and score
Using `G0` neighbors:

```text
IncidentCommitDegree(f) = Σ CommitCoChange(f,n)
IncidentTaskDegree(f)   = Σ TaskCoChange(f,n)
IC_f = Q(normalized(IncidentCommitDegree(f))) within f's primary-category cohort
IT_f = Q(normalized(IncidentTaskDegree(f)))   within f's primary-category cohort
K_f  = Q(alpha*IC_f + beta*IT_f)
```

Bottleneck components are:

```text
T_f = Q(normalized(IndependentTaskSpread(f)))
A_f = Q(normalized(distinct_authors(f)))
O_f = Q(normalized(independent_temporal_proximity(f)))
D_f = Q(normalized(distinct_neighbor_degree_G0(f)))
K_f = canonical centrality above

BottleneckScore(f) = Q(b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f)
```

Rankings are primary-category-local. Reports call this parallel-development
bottleneck/pressure and SHALL NOT claim actual merge conflict absent direct
separate evidence.

#### Scenario: Mixed endpoint cohorts
- **WHEN** a production file has production-production and production-tests `G0` edges
- **THEN** centrality uses raw incident evidence normalized in the production file cohort rather than summing incomparable edge-normalized weights

### Requirement: Deterministic OCP-pressure evidence
OCP uses canonical normalized `IndependentTaskSpread` and the same `G0`-derived
`K_f`.

Repeated independent editing is:

```text
Partners_f(t) = { u : (t,u) is independent for f }
PairExclusive_f(t,u) = { canonical commit c touching f : c references t and not u }
Qualifying_f(t) = SHA-deduplicated union of PairExclusive_f(t,u) over Partners_f(t)
Repeated_f(t) = max(|Qualifying_f(t)| - 1, 0)
E_f = sum(Repeated_f(t) for t with Partners_f(t) non-empty)
```

One commit counts at most once per task after the SHA union, even when it
qualifies against several partners. No independent pair means `E_f=0`.

#### Scenario: Task with multiple partners
- **WHEN** task `#101` is independently paired with `#102` and `#103`
- **THEN** its pair-exclusive sets are unioned and SHA-deduplicated before repeated edits are counted

### Requirement: Portable deterministic role-token evidence
Role hints operate on canonical filename stem using this ASCII tokenizer:

1. any character outside `[A-Za-z0-9]` delimits tokens;
2. split lowercase-letter → uppercase-letter transitions;
3. split before the final uppercase letter of an uppercase run when the next character is lowercase;
4. split letter ↔ digit transitions;
5. map ASCII `A-Z` to `a-z` by ordinal mapping.

Non-ASCII characters are delimiters. Matching uses exact token equality only;
substring, glob, regex, and culture-sensitive matching are forbidden.

Default tokens are `dispatcher`, `registry`, `handler`, `loader`, `session`,
`options`, `configuration`, `command`, `diagnostic`, `mapper`, `dto`, `model`,
`service`, and `orchestrator`. `N_f` is `1.000000000` when any token matches and
zero otherwise. Matched tokens report in canonical scalar-value string order.

```text
OcpPressureScore(f) = Q(o_t*T_f + o_c*K_f + o_r*Q(normalized(E_f)) + o_n*N_f)
```

OCP rankings are category-local. Findings use `OCP pressure` or `likely OCP
violation` with caveats and SHALL NOT claim formal proof.

#### Scenario: Role-token vectors
- **WHEN** stems include `OrderService`, `DiagnosticMapper`, `ViewModel`, `XMLParser2`, `Serviceable`, and `MyDispatcherFactory`
- **THEN** exact token matches include `service`, `diagnostic`, `mapper`, `model`, and `dispatcher`, while `Serviceable` does not match `service`

### Requirement: Stable rankings and refactoring investigations
Within one primary-category cohort, file findings rank by:

1. descending canonical score;
2. descending ordinary task spread;
3. descending churn;
4. descending commit count;
5. ascending canonical path by scalar-value ordering.

Cross-category file findings remain grouped in canonical category order.

Within one endpoint-category cohort, `G0` pairs rank by descending canonical
combined weight, commit component, task component, then canonical paths. Clusters
use the exact maximum/aggregate/path order above. Cross-cohort pair/cluster
results remain grouped.

Candidates are evidence-derived investigations, not automatic redesign decisions.
They retain source finding IDs, evidence/components, effective thresholds,
category/cohort identity, and caveats. Cluster-derived candidate logic uses
`Gtheta`; file scores remain `G0`-derived.

#### Scenario: Same-cohort total order
- **WHEN** same-cohort file findings tie on all numeric dimensions
- **THEN** canonical scalar-value path ordering is the final discriminator

### Requirement: Canonical JSON string escaping
Every canonical JSON string SHALL contain valid Unicode scalar values. Unpaired
surrogates or otherwise invalid internal Unicode SHALL fail serialization rather
than be replaced.

Escaping SHALL be exactly:

- U+0022 quotation mark => `\"`;
- U+005C reverse solidus => `\\`;
- U+0008/U+0009/U+000A/U+000C/U+000D => `\b`, `\t`, `\n`, `\f`, `\r`;
- other U+0000..U+001F => `\u00XX` with uppercase hexadecimal digits;
- solidus `/` remains unescaped;
- every other Unicode scalar, including non-ASCII, is emitted directly as UTF-8 and SHALL NOT be rewritten as optional `\uXXXX` or surrogate-pair escapes.

This applies equally to property names and string values. Unicode normalization
SHALL NOT be introduced during JSON serialization.

#### Scenario: Escaping vector
- **WHEN** canonical string content contains quote, backslash, slash, U+0001, newline, and `é`
- **THEN** each uses exactly the canonical escape/direct-UTF8 representation above

### Requirement: Deterministic report semantics and canonical JSON bytes
Markdown SHALL contain range/config summary, analyzed/excluded merge counts,
production hotspots, separate non-production groups, co-change cohorts,
bottlenecks, OCP pressure, candidates, and interpretation limits.

Canonical JSON SHALL include input/config/history-semantics identity, canonical
numeric scale, paths/aliases/categories, canonical file events and line-count
status, raw/canonical score components, weights/thresholds, `G0`/cluster cohort
identity, independent-task and centrality evidence, OCP evidence, enrichment
status where available, excluded merge count, and candidates.

Object properties SHALL serialize in the order declared by #243's versioned
schema. Dynamic object/map keys SHALL use canonical scalar-value string ordering.
Arrays SHALL preserve the category/cohort/ranking order defined by this capability.

Canonical JSON bytes SHALL use:

- UTF-8 without BOM;
- LF (`\n`) line endings;
- two-space indentation;
- no trailing whitespace;
- exactly one terminal LF;
- canonical JSON escaping above;
- exactly nine fractional digits for canonical real values;
- no exponent notation for canonical real values.

Canonical report artifact identity SHALL be over these exact bytes, not semantic
JSON equivalence or in-memory dictionary order.

Optional .NET/Roslyn enrichment is downstream; failure SHALL NOT drop, change,
rescore, or reorder Git-level findings.

Reports SHALL state that churn is not complexity; co-change is not module proof;
task/author evidence may be incomplete; multi-reference commits do not prove
independent work; excluded merge file deltas can understate merge-resolution
edits; exact rename misses rename-with-edit; exact renames contribute zero content
churn; NUL/gitlink/non-line events contribute zero line churn with explicit
status; strict UTF-8 is required for v1 Git paths; normalized scores compare only
inside their cohorts; role hints are bounded heuristics; and people decide
whether to refactor.

#### Scenario: Deterministic rendering
- **WHEN** identical canonical evidence is rendered by two conforming implementations
- **THEN** canonical JSON bytes are identical

### Requirement: Contributor theory reference
The repository SHALL contain an internal contributor reference consistent with
this capability, including reachability/merge semantics, strict UTF-8 path model,
scalar-value string ordering, exact rename identity, canonical file-event and LCS
line-churn semantics, category/cohort normalization, numeric/weight rules,
`G0/Gtheta`, cluster aggregation, exact epoch-second temporal proximity,
independent-task/repeated-edit semantics, cohort-safe centrality, ASCII role
tokenization, canonical JSON escaping/bytes, reports, ownership, and limitations.
Public MkDocs navigation SHALL NOT advertise the feature before implementation
ships.

#### Scenario: Contributor discovers theory
- **WHEN** a contributor opens the internal documentation index
- **THEN** it links to the Release Architecture Forensics theory reference
