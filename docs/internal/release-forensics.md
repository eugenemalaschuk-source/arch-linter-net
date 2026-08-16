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
authored refs and their resolved commit IDs. An empty range succeeds with an
explicit empty summary and zero/empty evidence.

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

- production
- tests
- docs
- generated
- build_ci
- samples_examples
- unknown

Alias classifications may be retained as evidence, but the canonical path owns
the primary category and the ranking path. `unknown` remains visible. #237 owns
the schema-backed matcher, ignore rules, category overrides, thresholds, and
other bounded `history_analysis` configuration.

Example: if `src/Old.cs` is unambiguously renamed to `tests/New.cs`, the range
contains one logical file, its canonical path is `tests/New.cs`, and its primary
category is derived from that path.

## Normalization populations

All score functions are total over non-negative populations:

~~~text
normalized(x) =
  0,                         if max(x in population) = 0
  x / max(x in population), otherwise

normalized_log(x) =
  0,                                         if max(x in population) = 0
  log(1 + x) / log(1 + max(x in population)), otherwise
~~~

Empty and all-zero populations produce finite `0`, never NaN, Infinity, an
exception, or an implementation-specific fallback. Missing optional evidence is
raw `0`; weights are not silently renormalized.

Population membership is part of the deterministic contract:

- #237 ignore rules are **analysis filters**; ignored logical files are removed
  before normalization and graph construction;
- presentation-only suppression never changes canonical scores;
- file-level metrics normalize against retained logical files in the same
  primary category;
- co-change edge components normalize against retained edges with the same
  unordered endpoint-category pair.

Therefore generated/docs/test/build noise cannot set a production normalization
maximum merely because those paths exist in the same Git range.

## Effective scoring configuration

The default profiles are:

| Profile | Default components and weights |
| --- | --- |
| Hotspot | commit 0.30, churn 0.25, task 0.25, author 0.10, temporal 0.10 |
| Bottleneck | independent task 0.35, author 0.15, temporal 0.20, degree 0.20, centrality 0.10 |
| OCP pressure | independent task 0.40, centrality 0.25, repeated episode edit 0.25, role hint 0.10 |
| Combined co-change | commit evidence 0.75, task evidence 0.25 |

These numbers are defaults, not hard-coded alternate formulas. #237 may expose
validated configuration for them. Every run has one effective profile and all
formulas consume those effective weights. Disabling a component is explicit in
the effective configuration; missing evidence never changes weights at runtime.

## Hotspots

For logical file `f`:

~~~text
C_f = normalized(commit_count(f))
H_f = normalized_log(churn(f))
T_f = normalized(distinct_task_references(f))
A_f = normalized(distinct_normalized_authors(f))
R_f = normalized(temporal_span_seconds(f))

HotspotScore(f) = w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f
~~~

The default `w_*` values are `.30,.25,.25,.10,.10`.
`temporal_span_seconds(f)` is the non-negative difference between latest and
earliest UTC Unix commit timestamps touching `f`. It measures persistent edit
pressure, not wall-clock recency; a one-commit file has span `0`.

Findings retain raw metrics, normalized components, and effective weights.

## Co-change

The co-change graph is weighted and undirected. Its vertices are retained
logical files. Pair paths are canonical paths in ascending ordinal order.

~~~text
CommitCoChange(a,b) = count(commits containing both a and b)
TaskCoChange(a,b)   = count(distinct tasks whose episodes contain both a and b)

CombinedCoChange(a,b) = alpha*normalized(CommitCoChange)
                        + beta*normalized(TaskCoChange)
~~~

The default `alpha=.75` and `beta=.25`; effective values come from the run's
validated profile. Missing task evidence contributes zero without changing the
weights.

A cluster is a connected component formed only from edges meeting an explicit
effective significance threshold. With no threshold, pair evidence remains but
the cluster list is empty rather than using an invented cutoff.

## Task episodes and independent-work evidence

A task episode is the commits linked to one extracted task/issue reference. A
single commit may reference several tasks and may contribute ordinary task spread
or task-level co-change to each. That does **not** establish parallel work.

For one logical file, task references `x` and `y` are an **independent task
pair** only when both sides have pair-exclusive evidence:

- at least one commit touching the file references `x` but not `y`;
- at least one commit touching the file references `y` but not `x`.

A commit referencing both tasks cannot establish independence, temporal overlap,
or repeated-episode-edit evidence for that pair.

~~~text
IndependentTaskSpread(f) =
  count(task refs participating in at least one independent pair for f)
~~~

For an independent pair, each interval is built from that side's pair-exclusive
commits:

~~~text
days_between(e1,e2) = 0                              if intervals overlap
                      ceil(positive UTC gap in days) otherwise

TemporalProximity(e1,e2) = 1 / (1 + days_between(e1,e2))
~~~

The file-level raw temporal value is the maximum proximity across independent
pairs, or `0` when none exists.

This prevents a commit such as `fix parser (#101, #102)` from producing a false
parallel-development signal merely because two references occur in one commit.
Ordinary hotspot task spread may still record both references.

## Bottleneck score

For bottlenecks:

~~~text
T_f = normalized(IndependentTaskSpread(f))
A_f = normalized(author spread)
O_f = normalized(independent-task temporal proximity)
D_f = normalized(distinct-neighbor degree)
K_f = normalized(weighted degree)

BottleneckScore(f) = b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f
~~~

The default `b_*` values are `.35,.15,.20,.20,.10`.

The result is described as parallel-development bottleneck/pressure. It does not
claim that a merge conflict actually occurred.

## OCP-pressure score and role tokens

OCP task spread also uses `IndependentTaskSpread`. `E_f` measures repeated
pair-exclusive editing across references that participate in at least one
independent pair: for each such reference, qualifying commits after its first
unique pair-exclusive commit are counted, with a commit counted at most once per
task reference. With no independent pair, `E_f = 0`.

Role/name evidence is deterministic. Start from the canonical file name without
its final extension. Tokenize by splitting on:

- non-alphanumeric characters;
- lower-to-upper camel/Pascal transitions;
- acronym-to-word boundaries;
- letter/digit boundaries.

Tokens are invariant-lowercase and are matched by **exact token equality only**.
There is no substring, glob, or regex matching.

Default tokens:

~~~text
dispatcher, registry, handler, loader, session, options, configuration,
command, diagnostic, mapper, dto, model, service, orchestrator
~~~

`N_f = 1` if at least one token matches, otherwise `0`. All matched tokens are
reported in ascending ordinal order. Thus `OrderService` matches `service`,
`DiagnosticMapper` matches `diagnostic` and `mapper`, and `ViewModel` matches
`model`; a token merely embedded inside an unsplit identifier does not match.

~~~text
OcpPressureScore(f) = o_t*T_f + o_c*K_f + o_r*normalized(E_f) + o_n*N_f
~~~

The default `o_*` values are `.40,.25,.25,.10`.

Role evidence is bounded heuristic evidence, not semantic proof. Reports say
`OCP pressure` or `likely OCP violation`, never `OCP violation proven` without
separate direct evidence.

## Ranking

File findings sort by:

1. descending score;
2. descending ordinary task spread;
3. descending churn;
4. descending commit count;
5. ascending canonical logical path.

Pairs sort by descending combined weight, commit weight, task weight, then
ascending first and second canonical paths. Clusters sort by descending maximum
edge, descending aggregate edge weight, then ascending first member canonical
path.

## Recommendations

Recommendations are investigations with source findings, component values,
effective thresholds, and caveats:

| Evidence | Candidate investigation |
| --- | --- |
| High OCP pressure plus role hint | Extract an extension point. |
| High co-change cluster | Revisit a module or contract boundary. |
| High bottleneck score | Split orchestration from feature-specific behavior. |
| High test-only hotspot | Improve fixture/helper architecture. |

Candidate thresholds belong to #237. If nothing qualifies, the deterministic
candidate collection is empty.

## Report semantics and limits

Markdown contains the analyzed range/effective configuration, hotspots,
co-change pairs/clusters, bottlenecks, OCP pressure, candidates, and limitations.
Canonical JSON contains input metadata, effective configuration, canonical paths
and aliases, primary categories, raw and normalized score components, effective
weights, co-change evidence, independent-task evidence, OCP evidence, and
candidates. Generated timestamps and environment-specific display data do not
alter canonical result identity.

The Git/history core remains independent of optional .NET/Roslyn enrichment.
Later enrichment may attach project, namespace, or type facts, but parse/mapping
failure never drops, changes, or reorders the file-level finding.

Every report must make these limits clear:

- churn is not complexity;
- incomplete task/author references can understate components;
- a multi-reference commit is not proof of independent workstreams;
- non-production categories can dominate their own raw volume but not a
  production normalization cohort;
- co-change does not prove module ownership;
- role/name hints are bounded heuristics;
- people decide whether evidence warrants a refactor.

## Ownership and non-goals

- #236 implements deterministic Git ingestion and the existing CLI command family.
- #237 owns normal policy schema/configuration, filtering, and path classification.
- #238/#239 implement hotspots and co-change independently.
- #240/#241 consume the defined independent-work evidence for bottleneck and OCP pressure.
- #242 adds optional .NET enrichment; #243 renders stable reports and candidates.

This document does not implement the analyzer, prove a formal design-law
violation, require LLM conclusions, create a separate product/configuration
authority, or freeze future scoring changes without reviewed specification work.
