# Release Architecture Forensics Theory

This contributor reference is the theory and functional-requirements authority
for Release Architecture Forensics introduced by #234. It is owned by #235 and
synchronized with the
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

## Canonical input and commit set

One analysis is identified by:

~~~text
(repository objects, explicit from ref, explicit to ref,
 effective history_analysis policy, tool version)
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

This conservative rule prevents one implementation from using first-parent
diffs, another combined diffs, and a third from double-counting branch changes.

## Logical files and exact rename recognition

The initial profile deliberately does **not** use ambient Git similarity rename
detection. A canonical rename is recognized only inside one non-merge commit when:

- one path is deleted;
- one path is added;
- the deleted preimage and added postimage have the same Git blob object ID;
- the relation is one-to-one, with no competing source or destination.

Similarity-based rename inference and copy inference do not participate in
canonical identity. A rename-with-edit is therefore intentionally not recognized
in v1. Split/copy/merge-like ambiguous relationships stay as separate logical
files.

A logical file is one linear chain of exact renames. Its canonical path is the
last in-range path, including a deleted path when deletion is final.

Aliases contain every distinct historical non-canonical path exactly once,
ordered by first in-range occurrence using canonical commit order and then path.
The canonical path is never duplicated in aliases.

Examples:

~~~text
A -> B -> C    => canonical C, aliases [A, B]
A -> B -> A    => canonical A, aliases [B]
A -> {B, C}    => no rename chain; identities remain separate
~~~

Churn is additions plus deletions summed over the logical identity; it is volume,
not complexity.

## Categories

Primary category derives from the canonical path. Canonical group order is:

1. production
2. tests
3. docs
4. generated
5. build_ci
6. samples_examples
7. unknown

Alias classifications may remain evidence but do not replace the primary
category. #237 owns bounded matching, ignores, overrides, thresholds, and the
schema-backed `history_analysis` configuration.

## Normalization populations

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
ranking, or canonical serialization. Implementations may use any higher-precision
internal algorithm but must agree on the correctly rounded mathematical value.

Canonical JSON writes exactly nine fractional digits, invariant culture, and no
exponent notation. Raw commit/task/author/churn counts and day gaps remain exact
integers.

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
most nine fractional digits; exponent notation is not canonical authoring.

A component is enabled iff its effective weight is greater than zero. Zero means
disabled. Evidence availability never changes enabledness.

At least one component is enabled and each exact decimal profile sums to
`1.000000000`. For co-change, `alpha + beta = 1.000000000`. Validation happens
before score quantization; `Q(v)` cannot repair a bad sum. Invalid profiles fail
instead of being rounded or rescaled.

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
committer timestamp; one touch has span zero.

Hotspots rank inside each category cohort. Production is the primary `top
hotspots` ranking; non-production groups are separate. A docs `0.95` cannot
outrank production `0.80` because the numbers come from different populations.

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

For each base edge:

~~~text
CommitComponent = Q(normalized(CommitCoChange))
TaskComponent   = Q(normalized(TaskCoChange))
CombinedCoChange = Q(alpha*CommitComponent + beta*TaskComponent)
~~~

Edge components normalize inside the edge's endpoint-category cohort. Pairs rank
inside that same cohort.

This distinction is intentional. For example:

~~~text
c1 #101 changes A only
c2 #101 changes B only
~~~

Then `TaskCoChange(A,B)=1` but `CommitCoChange(A,B)=0`, so there is no `G0` edge.

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

Clusters are connected components of `Gtheta` with at least two vertices, built
independently inside each endpoint-category cohort. No threshold means no
inferred clusters.

For cluster `C`:

~~~text
ClusterEdges(C) = qualifying Gtheta edges whose endpoints are members of C
ClusterMaximum(C) = max(CombinedCoChange(e) for e in ClusterEdges(C))
ClusterAggregate(C) = Q(sum(CombinedCoChange(e) for e in ClusterEdges(C)))
~~~

Sub-threshold internal `G0` edges do not contribute to the aggregate.

Example at `theta=0.600000000`:

~~~text
AB = 0.600000000
BC = 0.700000000
AC = 0.590000000
~~~

The cluster `{A,B,C}` has `ClusterMaximum=0.700000000` and
`ClusterAggregate=1.300000000`; `AC` is excluded.

Clusters rank inside their endpoint cohort by maximum, aggregate, then first
canonical member path.

## Independent task evidence

A multi-reference commit may contribute ordinary task spread/co-change but does
not prove independent work.

Refs `x,y` form an independent pair for file `f` only if each side has at least
one canonical file-touch commit referencing that ref but not the other.

Each pair-side interval is a **closed** interval from the minimum to maximum
committer epoch second of its pair-exclusive commits:

~~~text
IndependentTaskSpread(f) = refs participating in at least one independent pair
days_between = 0 when the closed intervals overlap,
               else ceil(positive UTC gap in days)
TemporalProximity(x,y) = Q(1/(1+days_between))
~~~

Shared-reference commits do not enter the pair intervals.

## Cohort-safe centrality and bottleneck score

Do **not** sum endpoint-cohort-normalized edge scores to obtain file centrality.
A file can have incident edges in several endpoint cohorts, so those edge scores
are not on one comparable scale.

Instead, using `G0`:

~~~text
IncidentCommitDegree(f) = Σ CommitCoChange(f,n) over G0 neighbors
IncidentTaskDegree(f)   = Σ TaskCoChange(f,n) over G0 neighbors
IC_f = Q(normalized(IncidentCommitDegree(f))) within f's primary-category cohort
IT_f = Q(normalized(IncidentTaskDegree(f)))   within f's primary-category cohort
K_f  = Q(alpha*IC_f + beta*IT_f)
~~~

The first profile intentionally reuses co-change `alpha/beta` for centrality;
there is no hidden second mix.

Then:

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

For task `t`:

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

The v1 tokenizer is ASCII-defined so different Unicode/library classifiers cannot
change `N_f`.

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

Expected vectors:

| Stem | Tokens | Matches |
| --- | --- | --- |
| `OrderService` | `order`, `service` | `service` |
| `DiagnosticMapper` | `diagnostic`, `mapper` | both |
| `ViewModel` | `view`, `model` | `model` |
| `XMLParser2` | `xml`, `parser`, `2` | none |
| `Serviceable` | `serviceable` | none |
| `MyDispatcherFactory` | `my`, `dispatcher`, `factory` | `dispatcher` |

`N_f = 1.000000000` when any role token matches, otherwise zero.

~~~text
OcpPressureScore(f) = Q(o_t*T_f + o_c*K_f + o_r*Q(normalized(E_f)) + o_n*N_f)
~~~

OCP rankings are category-local and are heuristic evidence, never formal proof.

## Ranking and candidates

Within a file category: descending canonical score, ordinary task spread, churn,
commit count, then canonical path. Category groups remain separate in fixed
category order.

Within an endpoint-category cohort, `G0` pairs rank by combined/commit/task
canonical weight then paths. Clusters rank by `ClusterMaximum`,
`ClusterAggregate`, then first member path. Cross-cohort results stay grouped.

Candidates carry source evidence, effective thresholds, category/cohort identity,
and caveats. Threshold comparison uses canonical values inside the finding's own
cohort. Cluster-derived candidates consume `Gtheta`; file scores remain `G0`
derived. High OCP plus role hint suggests extension-point investigation; high
co-change cluster suggests boundary investigation; high bottleneck suggests
orchestration/feature split; high test hotspot suggests fixture/helper work.

## Canonical JSON and report semantics

Markdown contains range/config summary, analyzed and excluded merge counts,
production hotspots, separate non-production rankings, co-change cohorts,
bottlenecks, OCP pressure, candidates, and limitations.

Canonical JSON contains identity, history-semantics identity, canonical numeric
scale, paths/aliases, categories, raw/canonical components, effective
weights/thresholds, `G0`/cluster cohort identity, independent-task and centrality
evidence, OCP evidence, excluded merge count, and candidates.

Arrays use the stable category/cohort order above. Object properties follow the
order declared by #243's versioned report schema. Dynamic map keys use ascending
ordinal key order after canonical string normalization.

Canonical JSON bytes use:

- UTF-8 without BOM;
- LF line endings;
- two-space indentation;
- no trailing whitespace;
- exactly one terminal LF;
- exactly nine fractional digits for canonical real values;
- no exponent notation for canonical real values.

Report artifact identity is over these canonical JSON bytes, not incidental
in-memory dictionary ordering.

Optional .NET/Roslyn enrichment is downstream and cannot drop, change, or reorder
file-level findings.

Reports must state that churn is not complexity; co-change is not module proof;
task/author evidence may be incomplete; multi-reference commits are not
independent-work proof; excluded merge deltas can understate merge-resolution
edits; exact-blob rename recognition intentionally misses rename-with-edit cases;
normalized scores are comparable only in their declared cohort; role hints are
bounded heuristics; and people decide whether to refactor.

## Verification vectors for downstream implementation

The implementation backlog should turn these theory boundaries into synthetic
Git/golden tests at minimum:

- `Range_Reachability`: side branch reachable from `to` but not `from` is included;
- `Merge_DoesNotDoubleCount`: merge is metadata-only for file evidence;
- `Rename_CrossCategory`: exact blob move preserves one logical identity;
- `Rename_SplitIsNotChain`: one-to-many move stays separate;
- `Rename_AliasCycleOrRepeat_Deduplicates`: `A→B→A` has alias `B` once;
- `TaskOnlyAssociation_DoesNotCreateBaseEdge`;
- `Threshold_DoesNotChangeCentrality`;
- `ClusterAggregate_UsesQualifyingEdgesOnly`;
- `RoleTokenizer_ExactNotSubstring`;
- `CanonicalJson_LocaleIndependent`;
- `InputEnumerationPermutation`;
- `EmptyRange_AllZero`.

## Ownership

- #236: deterministic Git ingestion, reachability, file deltas, exact rename identity, CLI family.
- #237: schema/config, path classification, ignores, thresholds, effective profiles.
- #238/#239: hotspot and `G0`/`Gtheta` co-change evidence.
- #240/#241: independent-task bottleneck/OCP evidence.
- #242: optional .NET enrichment.
- #243: stable grouped Markdown and canonical JSON reports/candidates.
