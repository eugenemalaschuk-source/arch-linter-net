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

## Canonical input

One analysis is identified by:

~~~text
(repository objects, explicit from ref, explicit to ref,
 effective history_analysis policy, tool version)
~~~

`from` is exclusive and `to` inclusive. Both refs must resolve; unknown or
ambiguous refs fail closed. Canonical evidence uses repository-relative `/`
paths, commits ordered by committer UTC timestamp then SHA, deterministic author
normalization and task refs, effective config identity, and tool version.
Checkout roots, generated timestamps, locale/timezone, process state, and
uncommitted changes are non-canonical.

## Logical files and categories

A logical file is one unambiguous linear rename chain. Its canonical path is the
last in-range path, including a deleted path when deletion is final. Earlier paths
remain aliases/evidence. Copies, splits, merges, and ambiguous rename relations
remain separate identities.

Churn is additions plus deletions summed over the logical identity; it is volume,
not complexity.

Primary category derives from the canonical path. Canonical group order is:

1. production
2. tests
3. docs
4. generated
5. build_ci
6. samples_examples
7. unknown

#237 owns bounded matching, ignores, overrides, thresholds, and schema-backed
`history_analysis` configuration.

## Normalization populations

#237 ignore rules are analysis filters applied before graph/score construction.
Presentation suppression is downstream and cannot change scores.

File-level metrics normalize against retained files in the same primary category.
Edge-level metrics normalize against retained edges with the same unordered
endpoint-category pair. Therefore non-production volume cannot set production
maxima, and normalized scores from separate cohorts are not globally comparable.

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
score, and configured numeric threshold is reduced to `Q(v)` before threshold
comparison, ranking, or canonical serialization. Implementations may use any
higher-precision internal algorithm but must agree on the correctly rounded
mathematical result.

Canonical JSON writes exactly nine fractional digits, invariant culture, and no
exponent notation. Raw commit/task/author/churn counts and day gaps remain exact
integers.

## Effective scoring configuration

Defaults:

| Profile | Weights |
| --- | --- |
| Hotspot | commit .30, churn .25, task .25, author .10, temporal .10 |
| Bottleneck | independent task .35, author .15, temporal .20, degree .20, centrality .10 |
| OCP | independent task .40, centrality .25, repeated edit .25, role hint .10 |
| Co-change | commit .75, task .25 |

Configured weights are finite non-negative base-10 decimals with at most nine
fractional digits. Enabled components are positive, disabled components zero, at
least one is enabled, and each profile sums exactly to `1.000000000`. For
co-change, `alpha + beta = 1.000000000`. Invalid profiles fail validation; they
are not repaired at runtime.

## Hotspots

~~~text
C_f = Q(normalized(commit_count(f)))
H_f = Q(normalized_log(churn(f)))
T_f = Q(normalized(distinct_task_refs(f)))
A_f = Q(normalized(distinct_authors(f)))
R_f = Q(normalized(temporal_span_seconds(f)))

HotspotScore(f) = Q(w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f)
~~~

`temporal_span_seconds` is latest minus earliest UTC commit timestamp; one commit
has span zero. Hotspots rank inside each category cohort. Production is the
primary `top hotspots` ranking; non-production groups are separate. A docs
`0.95` cannot outrank production `0.80` because they use different populations.

## Co-change and clusters

~~~text
CommitCoChange(a,b) = count(commits containing both)
TaskCoChange(a,b)   = count(distinct tasks containing both)
CommitComponent = Q(normalized(CommitCoChange))
TaskComponent   = Q(normalized(TaskCoChange))
CombinedCoChange = Q(alpha*CommitComponent + beta*TaskComponent)
~~~

Edge components normalize inside their endpoint-category cohort.

A configured significance threshold applies only to canonical
`CombinedCoChange` and uses inclusive comparison:

~~~text
edge qualifies iff CombinedCoChange >= threshold
~~~

Clusters are connected components with at least two vertices, built independently
inside each endpoint-category cohort. No threshold means no inferred clusters.
Pairs and clusters rank only within their cohort.

## Independent task evidence

A multi-reference commit may contribute ordinary task spread/co-change but does
not prove independent work.

Refs `x,y` form an independent pair for file `f` only if each side has at least
one file-touching commit referencing that ref but not the other.

~~~text
IndependentTaskSpread(f) = refs participating in at least one independent pair
TemporalProximity(x,y) = Q(1/(1+days_between(pair-exclusive intervals)))
~~~

Shared-reference commits do not establish independence or temporal overlap for
that pair.

## Cohort-safe centrality and bottleneck score

Do **not** sum endpoint-cohort-normalized edge scores to obtain file centrality.
A file can have incident edges in several endpoint cohorts, so those normalized
edge scores are not on one comparable scale.

Instead:

~~~text
IncidentCommitDegree(f) = Σ CommitCoChange(f,n) over retained neighbors
IncidentTaskDegree(f)   = Σ TaskCoChange(f,n) over retained neighbors
IC_f = Q(normalized(IncidentCommitDegree(f))) within f's primary-category cohort
IT_f = Q(normalized(IncidentTaskDegree(f)))   within f's primary-category cohort
K_f  = Q(alpha*IC_f + beta*IT_f)
~~~

Then:

~~~text
T_f = Q(normalized(IndependentTaskSpread(f)))
A_f = Q(normalized(author_spread(f)))
O_f = Q(normalized(independent_temporal_proximity(f)))
D_f = Q(normalized(distinct_neighbor_degree(f)))

BottleneckScore(f) = Q(b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f)
~~~

This creates one centrality scale per file category even when a production file
has both production-production and production-tests edges. Bottleneck rankings
remain category-local and describe pressure, not proof of merge conflicts.

## OCP pressure and repeated edits

OCP uses the same `IndependentTaskSpread` and cohort-safe `K_f`.

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

Role hints tokenize the canonical filename stem at non-alphanumeric,
camel/Pascal, acronym-to-word, and letter/digit boundaries. Tokens are
invariant-lowercase and match by exact equality only. Default tokens:

~~~text
dispatcher, registry, handler, loader, session, options, configuration,
command, diagnostic, mapper, dto, model, service, orchestrator
~~~

`N_f = 1.000000000` when any token matches, otherwise zero.

~~~text
OcpPressureScore(f) = Q(o_t*T_f + o_c*K_f + o_r*Q(normalized(E_f)) + o_n*N_f)
~~~

OCP rankings are category-local and are heuristic evidence, never formal proof.

## Ranking and candidates

Within a file category: descending canonical score, ordinary task spread, churn,
commit count, then canonical path. Category groups remain separate in the fixed
category order.

Within an endpoint-category cohort: pairs rank by combined/commit/task canonical
weight then paths; clusters by maximum edge, aggregate edge weight, then first
member path. Cross-cohort results remain grouped.

Candidates carry source evidence, effective thresholds, category/cohort identity,
and caveats. Threshold comparison uses canonical values inside the finding's own
cohort. High OCP plus role hint suggests extension-point investigation; high
co-change cluster suggests boundary investigation; high bottleneck suggests
orchestration/feature split; high test hotspot suggests fixture/helper work.

## Report semantics and limits

Markdown contains range/config summary, production hotspots, separate
non-production rankings, co-change cohorts, bottlenecks, OCP pressure, candidates,
and limitations.

Canonical JSON contains identity, canonical numeric scale, paths/aliases,
categories, raw/canonical components, effective weights/thresholds, co-change
cohort identity, independent-task and centrality evidence, OCP evidence, and
candidates. Canonical real values have exactly nine fractional digits.

Optional .NET/Roslyn enrichment is downstream and cannot drop, change, or reorder
file-level findings.

Reports must state that churn is not complexity; co-change is not module proof;
task/author evidence may be incomplete; multi-reference commits are not
independent-work proof; normalized scores are comparable only in their declared
cohort; role hints are bounded heuristics; and people decide whether to refactor.

## Ownership

- #236: deterministic Git ingestion and CLI family.
- #237: schema/config, path classification, ignores, thresholds, effective profiles.
- #238/#239: hotspot and co-change evidence.
- #240/#241: independent-task bottleneck/OCP evidence.
- #242: optional .NET enrichment.
- #243: stable grouped reports and candidates.
