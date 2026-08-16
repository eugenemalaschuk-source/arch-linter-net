# Release Architecture Forensics Theory

This contributor reference is the readable theory and functional-requirements
authority for Release Architecture Forensics introduced by #234. It is owned by
#235 and synchronized with the
[OpenSpec capability](../../openspec/specs/release-architecture-forensics/spec.md).

It documents planned behavior for #236–#243, not a currently shipped command or
policy surface.

## Product boundary

Current-state governance asks whether architecture is valid now. Release
forensics asks what architecture pressure accumulated in an explicit Git range,
which files became coordination bottlenecks, and what refactoring investigations
are justified by the evidence.

The result is evidence-backed pressure, not proof of a design-law violation.
Canonical scoring is deterministic and does not require LLM inference.

## Canonical analysis identity and range

One analysis is identified by:

~~~text
(repository objects, explicit from ref, explicit to ref,
 effective history_analysis policy, history-semantics profile, tool version)
~~~

`from` is exclusive and `to` inclusive. Both refs must resolve; unknown or
ambiguous refs fail closed. The analyzed commit set is exactly:

~~~text
Commits(from,to) = Reachable(to) \ Reachable(from)
~~~

`Reachable(r)` includes `r` and every commit reachable through parent edges. This
works even when `from` is not an ancestor of `to`.

Commits are ordered by committer UTC epoch-second timestamp, then full SHA in
ordinal order. All temporal metrics use those committer timestamps.

For an ordinary one-parent commit, file evidence is the parent-tree → commit-tree
delta. A root commit uses the empty Git tree as its parent.

For the first deterministic profile, merge commits remain visible in range
metadata but contribute **no file-touch evidence**. They therefore do not add
churn, file commit count, file author spread, file task membership, rename
relations, co-change edges, or downstream file scores. Reports expose the number
of excluded merges and warn that merge-resolution-only edits can be understated.

## Canonical Git paths

Git paths are bytes. V1 accepts only paths that decode as strict UTF-8. Invalid
UTF-8 fails closed before classification, rename chaining, ranking, or JSON.
There is no locale/code-page fallback and no replacement-character decoding.
Canonical paths are repository-relative and use `/` separators.

This restriction is deliberate: a deterministic error is safer than two
platforms inventing different canonical path text from the same Git bytes.

## Logical files and exact rename recognition

The initial profile deliberately does **not** use ambient Git similarity rename
detection. A canonical rename is recognized only inside one non-merge commit when:

- one path is deleted;
- one path is added;
- the deleted preimage and added postimage have the same Git blob object ID;
- the relation is one-to-one, with no competing source or destination.

Similarity-based rename inference and copy inference do not participate in
canonical identity. A rename-with-edit is intentionally not recognized in v1.
Split/copy/merge-like ambiguous relationships stay as separate logical files.

A logical file is one linear chain of exact renames. Its canonical path is the
last in-range path, including a deleted path when deletion is final. Aliases are
distinct non-canonical historical paths, ordered by first canonical occurrence
then ordinal path, and the canonical path is excluded from aliases.

Examples:

~~~text
A -> B -> C    => canonical C, aliases [A, B]
A -> B -> A    => canonical A, aliases [B]
A -> {B, C}    => no rename chain; identities remain separate
~~~

## Canonical file events and churn

After logical identity is built, there is one canonical file event per logical
file per canonical file-evidence commit.

A pure exact-blob rename collapses the raw delete/add pair into one `rename`
event. It still counts as one file touch, but content churn is zero:

~~~text
canonical_additions = 0
canonical_deletions = 0
canonical_churn     = 0
line_count_status   = exact_rename
~~~

This prevents a 100-line pure move from being interpreted by one backend as
roughly 200 lines of churn and by another as zero.

For an ordinary text event with meaningful line counts:

~~~text
line_count_status = text
churn(event) = additions + deletions
~~~

For a binary delta or any delta where meaningful line counts are unavailable:

~~~text
canonical_additions = 0
canonical_deletions = 0
line_count_status   = binary_or_unavailable
~~~

V1 never substitutes bytes, estimated lines, textconv output, or backend sentinel
values for unavailable line counts.

File churn is the sum of canonical additions plus deletions across canonical file
events. `commit_count(f)` is the number of **distinct canonical file-evidence
commits** touching the logical file, not the number of raw delta entries.

Churn is volume, not complexity. Exact renames and binary/unavailable line counts
can therefore understate physical file-size movement/change and must be disclosed
as interpretation limits.

## Categories and normalization populations

Primary category derives from the canonical path. Canonical group order is:

1. production
2. tests
3. docs
4. generated
5. build_ci
6. samples_examples
7. unknown

#237 ignore rules are analysis filters applied before graph/score construction.
Presentation suppression is downstream and cannot change scores.

File-level metrics normalize against retained files in the same primary category.
Base-edge metrics normalize against base co-change edges with the same unordered
endpoint-category pair. Therefore non-production volume cannot set production
maxima, and normalized values from different cohorts are not globally comparable.

Mathematical normalizers are:

~~~text
normalized(x) = 0 if max(population)=0, else x/max(population)
normalized_log(x) = 0 if max(population)=0,
                    else log(1+x)/log(1+max(population))
~~~

Missing optional evidence is raw zero; weights are never silently renormalized.

## Canonical numeric model

All canonical derived real values use:

~~~text
Q(v) = round-half-to-even(v, 9 decimal places)
~~~

Every normalized component, temporal proximity, combined edge weight, final
score, and numeric threshold is reduced to `Q(v)` before threshold comparison,
ranking, or canonical serialization.

Useful golden vectors:

~~~text
Q(1.2345678905) = 1.234567890
Q(1.2345678915) = 1.234567892
Q(log(51)/log(101)) = 0.851944303
~~~

## Effective scoring configuration

Defaults:

| Profile | Weights |
| --- | --- |
| Hotspot | commit .30, churn .25, task .25, author .10, temporal .10 |
| Bottleneck | independent task .35, author .15, temporal .20, degree .20, centrality .10 |
| OCP | independent task .40, centrality .25, repeated edit .25, role hint .10 |
| Co-change | commit .75, task .25 |

Configured weights are finite non-negative ordinary base-10 decimals with at
most nine fractional digits; exponent notation is not canonical authoring. A
component is enabled iff its effective weight is greater than zero. Zero means
disabled. Evidence availability never changes enabledness.

At least one component is enabled and each exact decimal profile sums to
`1.000000000`. For co-change, `alpha + beta = 1.000000000`. Validation happens
before score quantization; `Q(v)` cannot repair a bad sum.

## Hotspots

~~~text
C_f = Q(normalized(commit_count(f)))
H_f = Q(normalized_log(churn(f)))
T_f = Q(normalized(distinct_task_refs(f)))
A_f = Q(normalized(distinct_authors(f)))
R_f = Q(normalized(temporal_span_seconds(f)))

HotspotScore(f) = Q(w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f)
~~~

`temporal_span_seconds` is latest minus earliest canonical file-evidence
committer timestamp; one touch has span zero. Hotspots rank inside each category
cohort. Production is the primary `top hotspots` ranking; non-production groups
are separate.

## Base co-change graph `G0`

The stable evidence/scoring graph is:

~~~text
G0 = (V, E0)
V  = retained logical files
E0 = { (a,b) : a != b and CommitCoChange(a,b) > 0 }

CommitCoChange(a,b) = count(canonical file-evidence commits containing both)
TaskCoChange(a,b)   = count(distinct tasks whose file episodes contain both)
~~~

Task co-change can weight an existing base edge but does not create a base edge
when the files never changed in the same canonical file-evidence commit.

~~~text
CommitComponent = Q(normalized(CommitCoChange))
TaskComponent   = Q(normalized(TaskCoChange))
CombinedCoChange = Q(alpha*CommitComponent + beta*TaskComponent)
~~~

All distinct-neighbor and centrality evidence (`D_f`, incident degrees, `K_f`)
uses `G0`.

## Threshold graph `Gtheta` and clusters

A configured significance threshold applies only to canonical
`CombinedCoChange`, with inclusive comparison:

~~~text
Gtheta = (V, { e in E0 : CombinedCoChange(e) >= theta })
~~~

`Gtheta` exists only for cluster construction and cluster-derived candidate
logic. Changing `theta` cannot change edge normalization, pair rankings, `D_f`,
`K_f`, hotspots, bottlenecks, or OCP scores.

For cluster `C`:

~~~text
ClusterEdges(C) = qualifying Gtheta edges whose endpoints are members of C
ClusterMaximum(C) = max(CombinedCoChange(e) for e in ClusterEdges(C))
ClusterAggregate(C) = Q(sum(CombinedCoChange(e) for e in ClusterEdges(C)))
~~~

Sub-threshold internal `G0` edges do not contribute to the aggregate.

## Independent task evidence and temporal proximity

A multi-reference commit may contribute ordinary task spread/co-change but does
not prove independent work. Refs `x,y` form an independent pair for file `f`
only if each side has at least one canonical file-touch commit referencing that
ref but not the other.

Each pair-side interval is a closed interval from the minimum to maximum
committer epoch second of its pair-exclusive commits. Shared-reference commits do
not enter those intervals.

For non-overlapping intervals:

~~~text
gap_seconds = later.start_epoch_second - earlier.end_epoch_second

days_between = 0                         if gap_seconds <= 0
               ceil(gap_seconds / 86400) if gap_seconds > 0

TemporalProximity(x,y) = Q(1/(1+days_between))
~~~

For positive integer gaps, `(gap_seconds + 86399) div 86400` is equivalent.
Calendar-day difference, local midnight, timezone, and DST never participate.
A 25-hour (`90000` second) gap therefore yields `days_between=2`.

## Cohort-safe centrality and bottleneck score

Do **not** sum endpoint-cohort-normalized edge scores to obtain file centrality.
Using `G0`:

~~~text
IncidentCommitDegree(f) = Σ CommitCoChange(f,n) over G0 neighbors
IncidentTaskDegree(f)   = Σ TaskCoChange(f,n) over G0 neighbors
IC_f = Q(normalized(IncidentCommitDegree(f))) within f's primary-category cohort
IT_f = Q(normalized(IncidentTaskDegree(f)))   within f's primary-category cohort
K_f  = Q(alpha*IC_f + beta*IT_f)
~~~

The first profile intentionally reuses co-change `alpha/beta` for centrality.

~~~text
T_f = Q(normalized(IndependentTaskSpread(f)))
A_f = Q(normalized(author_spread(f)))
O_f = Q(normalized(independent_temporal_proximity(f)))
D_f = Q(normalized(distinct_neighbor_degree_G0(f)))

BottleneckScore(f) = Q(b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f)
~~~

Bottleneck rankings remain category-local and describe pressure, not proof of
merge conflicts.

## OCP pressure and repeated edits

OCP uses the same `IndependentTaskSpread` and `G0`-derived cohort-safe `K_f`.

~~~text
Partners_f(t) = {u : (t,u) independent for f}
PairExclusive_f(t,u) = {c touching f : c references t and not u}
Qualifying_f(t) = SHA-deduplicated union over Partners_f(t)
Repeated_f(t) = max(|Qualifying_f(t)| - 1, 0)
E_f = Σ Repeated_f(t)
~~~

Thus a task independent from several partners has one deterministic union and a
commit counts at most once per task reference.

## Portable role-token evidence

Starting from the canonical filename stem:

1. characters outside `[A-Za-z0-9]` delimit tokens;
2. split lowercase → uppercase transitions;
3. split before the final uppercase letter of an uppercase run when the next character is lowercase;
4. split letter ↔ digit transitions;
5. map ASCII `A-Z` to `a-z` by ordinal mapping.

Non-ASCII characters are delimiters. Matching is exact equality only; substring,
glob, regex, and culture-sensitive matching are forbidden.

Default tokens:

~~~text
dispatcher, registry, handler, loader, session, options, configuration,
command, diagnostic, mapper, dto, model, service, orchestrator
~~~

`N_f = 1.000000000` when any role token matches, otherwise zero.

~~~text
OcpPressureScore(f) = Q(o_t*T_f + o_c*K_f + o_r*Q(normalized(E_f)) + o_n*N_f)
~~~

## Ranking and candidates

Within a file category: descending canonical score, ordinary task spread, churn,
commit count, then canonical path. Category groups remain separate in fixed
category order.

Within an endpoint-category cohort, `G0` pairs rank by combined/commit/task
canonical weight then paths. Clusters rank by `ClusterMaximum`,
`ClusterAggregate`, then first member path.

Candidates carry source evidence, effective thresholds, category/cohort identity,
and caveats. Cluster-derived candidates consume `Gtheta`; file scores remain
`G0`-derived.

## Canonical JSON string and byte profile

Canonical strings are valid Unicode scalar sequences. V1 Git paths become such
strings only by strict UTF-8 decoding.

Canonical JSON string escaping is fixed:

- quote → `\"`;
- backslash → `\\`;
- U+0008/U+0009/U+000A/U+000C/U+000D → `\b`, `\t`, `\n`, `\f`, `\r`;
- other U+0000..U+001F controls → `\u00XX` with uppercase hex;
- `/` remains literal;
- all other Unicode scalars, including non-ASCII, are emitted directly as UTF-8
  and are not rewritten as `\uXXXX`/surrogate escapes.

Canonical JSON bytes additionally use:

- UTF-8 without BOM;
- LF line endings;
- two-space indentation;
- no trailing whitespace;
- exactly one terminal LF;
- exactly nine fractional digits for canonical real values;
- no exponent notation for canonical real values;
- versioned #243 property order;
- ascending ordinal order for dynamic map keys.

Report artifact identity is over these bytes, not incidental dictionary ordering.

## Report semantics and limitations

Markdown and JSON expose analyzed/excluded merge counts, history-semantics
identity, categories/cohorts, raw and normalized components, weights/thresholds,
`G0`/clusters, bottleneck/OCP evidence, line-count limitation markers, and
candidates.

Optional .NET/Roslyn enrichment is downstream and cannot drop, change, or reorder
file-level findings.

Reports must state that:

- churn is not complexity;
- co-change is not module proof;
- task/author evidence may be incomplete;
- multi-reference commits are not independent-work proof;
- excluded merge deltas can understate merge-resolution edits;
- exact-blob rename recognition misses rename-with-edit;
- exact renames contribute zero content churn;
- binary/unavailable line counts contribute zero churn;
- v1 requires strict UTF-8 Git paths;
- normalized scores compare only inside their declared cohort;
- role hints are bounded heuristics;
- people decide whether to refactor.

## Verification vectors for downstream implementation

At minimum implement synthetic/golden tests for:

- reachability independent of traversal order;
- root empty-tree delta;
- merge metadata without file double-counting;
- pure 100-line exact rename => one touch and zero churn;
- binary/unavailable line counts => zero additions/deletions plus marker;
- commit count => distinct canonical file-evidence commits;
- strict UTF-8 Git path rejection;
- exact rename cross-category, split, and alias-cycle behavior;
- category isolation;
- half-even and normalized-log vectors;
- task-only episode not creating a `G0` edge;
- threshold changes not affecting `D_f`/`K_f`/file scores;
- cluster aggregate using qualifying edges only;
- multi-reference false-positive controls and repeated-edit SHA union;
- 25-hour pair-exclusive gap => `days_between=2`;
- mixed endpoint-category centrality;
- ASCII role-token vectors;
- canonical JSON strings containing quote, backslash, slash, control and non-ASCII scalar;
- locale/enumeration-independent canonical JSON bytes;
- empty range/all-zero evidence.

## Ownership

- #236: deterministic Git ingestion, reachability, file deltas, canonical file events, strict UTF-8 path handling, exact rename identity, task refs, CLI family.
- #237: schema/config, path classification, ignores, thresholds, effective profiles.
- #238/#239: hotspot and `G0`/`Gtheta` co-change evidence.
- #240/#241: independent-task bottleneck/OCP evidence and exact temporal formula.
- #242: optional .NET enrichment.
- #243: stable grouped Markdown and canonical JSON schema/bytes/candidates.
