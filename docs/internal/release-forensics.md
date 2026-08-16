# Release Architecture Forensics Theory

This contributor reference is the theory and functional-requirements authority
for the Release Architecture Forensics work introduced by #234. It is owned by
#235 and synchronized with the
[Release Architecture Forensics OpenSpec capability](../../openspec/specs/release-architecture-forensics/spec.md).

It documents planned behavior for #236–#243. It does not describe a currently
shipped command, policy field, or report format.

## Product boundary

Current-state architecture governance asks:

> Is the architecture valid now?

Release forensics asks:

> What architecture pressure accumulated in this Git range, which files became
> coordination bottlenecks, and what refactoring investigations are justified
> by that evidence?

The result is evidence-backed pressure, not proof of a design-law violation.
Canonical scoring is deterministic and does not depend on an LLM or other
stochastic inference.

## Canonical input

One analysis is identified by:

~~~text
(repository objects, explicit from ref, explicit to ref,
 effective history_analysis policy, tool version)
~~~

`from` is exclusive and `to` is inclusive. Both refs must resolve before
analysis; unknown or ambiguous refs fail closed. Canonical metadata retains the
authored refs and resolved commit IDs. An empty range succeeds with explicit
empty/zero evidence.

Canonical evidence uses repository-relative paths with `/`, commit records
ordered by committer UTC timestamp then ordinal SHA, normalized author
identities, ordered task references, effective configuration identity, and tool
version. Absolute checkout paths, generated timestamps, locale, timezone,
machine/process state, and uncommitted working-tree changes are not canonical.

An author identity is the trimmed invariant-lowercase email when present,
otherwise the trimmed invariant-lowercase author name, otherwise `unknown`.

## Logical files, renames, and categories

A logical file is one unambiguous linear rename chain. Its **canonical logical
path** is the normalized path at the last in-range occurrence of the chain. If
the last occurrence is a deletion, the deleted path is canonical. Earlier paths
remain ordered aliases/evidence but do not receive independent scores.

Copies, splits, merges, or otherwise ambiguous rename relationships are not
collapsed. Each affected path remains a separate logical identity unless Git
evidence establishes one unambiguous chain.

Each commit-file record retains status, additions, deletions, the observed path,
and logical-file identity:

~~~text
churn(file, range) = Σ(additions(file, commit) + deletions(file, commit))
~~~

Churn is change volume, not complexity.

Every logical file has exactly one primary category derived from its canonical
logical path:

1. production
2. tests
3. docs
4. generated
5. build_ci
6. samples_examples
7. unknown

This is also the canonical serialization/group order. Alias classifications may
remain evidence, but the canonical path owns the primary category and ranking
path. `unknown` remains visible. #237 owns schema-backed matcher/ignore rules,
category overrides, thresholds, and the bounded `history_analysis` configuration.

## Normalization populations

Ignore rules are **analysis filters**: ignored logical files are removed before
normalization and graph construction. Presentation-only suppression never changes
canonical scores.

File-level components normalize against retained files in the same primary
category. Co-change edge components normalize against retained edges having the
same unordered endpoint-category pair. The endpoint-category pair is ordered by
the category order above.

This prevents generated/docs/test/build volume from setting production maxima.
It also means normalized values from separate cohorts are **not numerically
comparable** as one global ranking.

The mathematical normalizers are:

~~~text
normalized(x) =
  0,                         if max(population) = 0
  x / max(population), otherwise

normalized_log(x) =
  0,                                         if max(population) = 0
  log(1 + x) / log(1 + max(population)), otherwise
~~~

Empty/all-zero populations produce finite zero. Missing optional evidence is raw
zero; weights are never silently renormalized.

## Canonical numeric model

All canonical derived real values use nine decimal places:

~~~text
Q(v) = round-half-to-even(v, 9 decimal places)
~~~

The formulas define mathematical real values. Implementations may use any
higher-precision or equivalent internal algorithm, but every normalized
component, temporal proximity, combined edge weight, final score, and configured
numeric threshold is reduced to `Q(v)` **before** threshold comparison, ranking,
or canonical serialization.

Canonical JSON writes those values with exactly nine fractional digits,
invariant culture, and no exponent notation. Raw integral evidence such as
commit count, churn, task count, author count, and day gaps remains integer data.

This makes canonical output independent of platform-specific floating-point or
`log` implementation details: implementations must agree on the correctly
rounded mathematical result rather than on an intermediate machine representation.

## Effective scoring configuration

Default profiles are:

| Profile | Default components and weights |
| --- | --- |
| Hotspot | commit 0.30, churn 0.25, task 0.25, author 0.10, temporal 0.10 |
| Bottleneck | independent task 0.35, author 0.15, temporal 0.20, degree 0.20, centrality 0.10 |
| OCP pressure | independent task 0.40, centrality 0.25, repeated episode edit 0.25, role hint 0.10 |
| Combined co-change | commit 0.75, task 0.25 |

#237 may expose validated configuration for those weights. Every effective weight
is a finite non-negative base-10 decimal with at most nine fractional digits.
Within one profile:

- enabled components have weight `> 0`;
- disabled components have weight exactly `0`;
- at least one component is enabled;
- all effective weights sum exactly to `1.000000000`.

For co-change this means `alpha + beta = 1.000000000`. Invalid profiles fail
validation; they are not repaired by runtime normalization. Missing evidence does
not alter the effective profile.

## Hotspots

For logical file `f`:

~~~text
C_f = Q(normalized(commit_count(f)))
H_f = Q(normalized_log(churn(f)))
T_f = Q(normalized(distinct_task_references(f)))
A_f = Q(normalized(distinct_normalized_authors(f)))
R_f = Q(normalized(temporal_span_seconds(f)))

HotspotScore(f) = Q(w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f)
~~~

The default `w_*` values are `.30,.25,.25,.10,.10`.
`temporal_span_seconds(f)` is latest minus earliest UTC Unix commit timestamp;
a one-commit file has span zero.

Hotspots are ranked **inside each category cohort**. The default human `top
hotspots` section means production hotspots. Tests/docs/generated/build/sample/
unknown findings are separate ranked groups. A docs score of `0.950000000` does
not outrank a production score of `0.800000000`; their maxima came from different
populations.

## Co-change

The co-change graph is weighted and undirected. Vertices are retained logical
files; pair paths are canonical paths in ascending ordinal order.

~~~text
CommitCoChange(a,b) = count(commits containing both a and b)
TaskCoChange(a,b)   = count(distinct tasks whose episodes contain both a and b)

CommitComponent = Q(normalized(CommitCoChange))
TaskComponent   = Q(normalized(TaskCoChange))
CombinedCoChange = Q(alpha*CommitComponent + beta*TaskComponent)
~~~

Defaults are `alpha=.75`, `beta=.25`.

A configured co-change significance threshold is a canonical value in `[0,1]`
and applies **only to canonical `CombinedCoChange`**. The comparison is inclusive:

~~~text
edge qualifies iff CombinedCoChange >= threshold
~~~

Clusters are built independently for each unordered endpoint-category cohort
from qualifying edges in that cohort. A cluster is a connected component with at
least two vertices. If no threshold is configured, pair evidence remains and the
cluster list is empty.

Pairs and clusters are ranked only within their endpoint-category cohort; there
is no global numeric comparison across cohorts.

## Task episodes and independent-work evidence

A task episode is the commits linked to one extracted task/issue reference. A
single commit may reference several tasks and may contribute ordinary task spread
or task-level co-change to each. That does **not** establish parallel work.

For one logical file, references `x` and `y` are an **independent pair** only when
both sides have pair-exclusive evidence:

- at least one file-touching commit references `x` but not `y`;
- at least one file-touching commit references `y` but not `x`.

A commit referencing both tasks cannot establish independence or temporal overlap
for that pair.

~~~text
IndependentTaskSpread(f) =
  count(task refs participating in at least one independent pair for f)
~~~

For an independent pair, each interval is built from pair-exclusive commits:

~~~text
days_between = 0                              if intervals overlap
               ceil(positive UTC gap in days) otherwise

TemporalProximity = Q(1 / (1 + days_between))
~~~

The file temporal value is the maximum canonical proximity across independent
pairs, or zero when none exists.

## Bottleneck score

~~~text
T_f = Q(normalized(IndependentTaskSpread(f)))
A_f = Q(normalized(author spread))
O_f = Q(normalized(independent-task temporal proximity))
D_f = Q(normalized(distinct-neighbor degree))
K_f = Q(normalized(weighted degree))

BottleneckScore(f) = Q(b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f)
~~~

The default `b_*` values are `.35,.15,.20,.20,.10`.

Bottleneck rankings are category-local. The result is parallel-development
bottleneck/pressure evidence, not proof that a merge conflict occurred.

## OCP-pressure score and repeated editing

OCP task spread also uses `IndependentTaskSpread`.

For a task reference `t`:

~~~text
Partners_f(t) = { u : (t,u) is an independent pair for f }
PairExclusive_f(t,u) = { commit c touching f : c references t and not u }
Qualifying_f(t) = union(PairExclusive_f(t,u) for u in Partners_f(t)), deduplicated by SHA
Repeated_f(t) = max(|Qualifying_f(t)| - 1, 0)
E_f = sum(Repeated_f(t) for t with Partners_f(t) non-empty)
~~~

This resolves the multi-pair case explicitly. If `#101` is independent from both
`#102` and `#103`, its qualifying commit sets are unioned and SHA-deduplicated;
a commit contributes at most once to `Repeated_f(#101)`. The same SHA may count
once for two different task references if it independently qualifies for each.
If no independent pair exists, `E_f = 0`.

Role/name evidence starts from the canonical filename without its final extension.
Tokenize by:

- non-alphanumeric boundaries;
- lower-to-upper camel/Pascal transitions;
- acronym-to-word boundaries;
- letter/digit boundaries.

Tokens are invariant-lowercase and use exact equality only. No substring, glob,
or regex matching.

Default tokens are:

~~~text
dispatcher, registry, handler, loader, session, options, configuration,
command, diagnostic, mapper, dto, model, service, orchestrator
~~~

`N_f = 1.000000000` if at least one token matches, otherwise `0.000000000`.
Matched tokens are reported in ascending ordinal order.

~~~text
OcpPressureScore(f) = Q(o_t*T_f + o_c*K_f + o_r*Q(normalized(E_f)) + o_n*N_f)
~~~

The default `o_*` values are `.40,.25,.25,.10`. OCP rankings are category-local.
Reports say `OCP pressure` or `likely OCP violation`, never formal proof.

## Ranking and candidates

Within one primary-category cohort, file findings sort by:

1. descending canonical score;
2. descending ordinary task spread;
3. descending churn;
4. descending commit count;
5. ascending canonical logical path.

Category groups use the fixed category order and are not interleaved by score.

Within one endpoint-category cohort, pairs sort by descending canonical combined
weight, commit component, task component, then paths. Clusters sort by descending
canonical maximum edge, canonical sum of member-edge weights, then first member
path. Endpoint-category groups stay separate.

Recommendations are investigations with source findings, components, effective
thresholds, category/cohort identity, and caveats:

| Evidence | Candidate investigation |
| --- | --- |
| High OCP pressure plus role hint | Extract an extension point. |
| High co-change cluster | Revisit a module or contract boundary. |
| High bottleneck score | Split orchestration from feature-specific behavior. |
| High test-only hotspot | Improve fixture/helper architecture. |

Candidate thresholds compare canonical quantized values inside the finding's own
cohort. There is no cross-category claim that a larger normalized score means
more absolute architecture pressure. Empty qualifying sets remain empty; the tool
does not fabricate recommendations.

## Report semantics and limits

Markdown contains the analyzed range/effective configuration, production hotspot
ranking, separate non-production rankings, co-change cohort groups, bottlenecks,
OCP pressure, candidates, and limitations.

Canonical JSON contains input/config identity, canonical numeric scale, canonical
paths/aliases, primary categories, raw and canonical score components, effective
weights/thresholds, co-change cohort identity, independent-task evidence, OCP
evidence, and candidates. Arrays follow category/cohort grouping and stable
within-group ordering. Canonical real numbers have exactly nine fractional digits
and no exponent notation.

The Git/history core remains independent of optional .NET/Roslyn enrichment.
Later enrichment may attach project, namespace, or type facts, but mapping failure
never drops, changes, or reorders a file-level finding.

Every report must make clear that:

- churn is not complexity;
- co-change does not prove module ownership;
- task/author references may be incomplete;
- multi-reference commits are not independent-work proof;
- normalized scores are comparable only inside their declared category/cohort;
- role hints are bounded heuristics;
- people decide whether evidence warrants a refactor.

## Ownership and non-goals

- #236 implements deterministic Git ingestion and the existing CLI command family.
- #237 owns normal policy schema/configuration, path classification, thresholds, and effective profiles.
- #238/#239 implement hotspots and co-change independently.
- #240/#241 consume independent-task evidence for bottleneck and OCP pressure.
- #242 adds optional .NET enrichment; #243 renders stable reports and candidates.

This document does not implement the analyzer, prove a formal design-law
violation, require LLM conclusions, create a separate product/configuration
authority, or freeze future scoring changes without reviewed specification work.
