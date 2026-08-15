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
Canonical scoring is deterministic: it does not depend on an LLM or other
stochastic inference.

## Canonical input and entities

One analysis is identified by:

~~~text
(repository objects, explicit from ref, explicit to ref,
 effective history_analysis policy, tool version)
~~~

From is exclusive and to is inclusive. Both refs must resolve before analysis;
unknown or ambiguous refs fail closed. Canonical metadata retains the authored
refs and their resolved commit IDs. An empty range succeeds with an explicit
empty summary and zero/empty evidence.

Canonical evidence includes repository-relative logical paths using /, a commit
sequence ordered by committer UTC timestamp then ordinal SHA, normalized author
identities, extracted and ordered task/issue references, effective configuration
identity, and tool version. It excludes absolute checkout paths, generated
timestamps, timezone, locale, machine/process state, and uncommitted
working-tree changes.

| Term | Meaning |
| --- | --- |
| Logical file | One repository-relative file identity after unambiguous rename-chain normalization. |
| Task episode | Commits linked to one extracted task/issue reference. |
| Churn | Additions plus deletions across a logical file; volume, not complexity. |
| Hotspot | A file with repeated change pressure. |
| Co-change edge | An undirected relation between two logical files changed together. |
| Bottleneck | A file where workstream and graph evidence indicate coordination pressure. |
| OCP pressure | Heuristic evidence that unrelated work repeatedly modifies a likely extension or dispatch surface. |
| Refactoring candidate | An evidence-derived investigation, not an automatic redesign. |

An author identity is the trimmed, invariant-lowercase email when present;
otherwise it is the trimmed, invariant-lowercase author name; an absent value is
unknown. Task references are extracted, deduplicated, and sorted according to
the effective configuration.

Each commit-file record retains Git status, additions, deletions, and its
logical-file identity. An unambiguous rename belongs to one logical file:

~~~text
churn(file, range) = Σ(additions(file, commit) + deletions(file, commit))
~~~

## Normalization and configuration

All score functions are total over non-negative populations:

~~~text
normalized(x) =
  0,                         if max(x in population) = 0
  x / max(x in population), otherwise

normalized_log(x) =
  0,                                         if max(x in population) = 0
  log(1 + x) / log(1 + max(x in population)), otherwise
~~~

Empty and all-zero populations yield finite 0, never NaN, Infinity, an
exception, or an implementation-dependent fallback. Missing optional evidence
is raw 0: no task references means every task-dependent component is zero. The
remaining weights are never silently renormalized. Disabling a component is
allowed only through explicit, validated configuration.

The initial effective profiles are:

| Profile | Components and weights |
| --- | --- |
| Hotspot | commit 0.30, churn 0.25, task 0.25, author 0.10, temporal 0.10 |
| Bottleneck | task 0.35, author 0.15, temporal 0.20, degree 0.20, centrality 0.10 |
| OCP pressure | task 0.40, centrality 0.25, repeated episode edit 0.25, role hint 0.10 |
| Combined co-change | commit evidence 0.75, task evidence 0.25 |

Issue #237 owns the normal, schema-backed history_analysis configuration,
bounded reference/path matching, path overrides, and threshold validation. This
theory deliberately creates no second configuration authority.

## Path categories

Every path has exactly one category:

- production
- tests
- docs
- generated
- build_ci
- samples_examples
- unknown

Production findings are primary architecture signals. Other categories are
available for separate reporting or policy-controlled suppression. Unknown
remains visible and is never discarded. Classification uses normalized,
repository-relative logical paths, never an absolute checkout root.

## Hotspots and co-change

For logical file f:

~~~text
C_f = normalized(commit_count(f))
H_f = normalized_log(churn(f))
T_f = normalized(distinct_task_references(f))
A_f = normalized(distinct_normalized_authors(f))
R_f = normalized(temporal_span_seconds(f))

HotspotScore(f) = .30*C_f + .25*H_f + .25*T_f + .10*A_f + .10*R_f
~~~

temporal_span_seconds(f) is the non-negative difference between the latest and
earliest UTC Unix commit timestamps touching f. It measures persistent edit
pressure, not wall-clock recency; a one-commit file has span 0.

The co-change graph is weighted and undirected. Its vertices are logical files;
edge paths are in ascending ordinal order. For distinct a and b:

~~~text
CommitCoChange(a,b) = count(commits containing both a and b)
TaskCoChange(a,b)   = count(distinct tasks whose episodes contain both a and b)

CombinedCoChange(a,b) = .75*normalized(CommitCoChange)
                        + .25*normalized(TaskCoChange)
~~~

Absent task evidence contributes zero with unchanged weights. High co-change is
a coupling/coordination signal, not proof that files belong in one module.

A cluster is a connected component formed only from edges meeting an explicit
effective significance threshold. With no threshold, the result reports stable
pair evidence and an empty cluster list instead of inferring an arbitrary cutoff.

## Bottleneck and OCP-pressure evidence

For two different task episodes touching a file, the episode intervals overlap
when their UTC time intervals intersect. Otherwise their gap rounds up to whole
days:

~~~text
days_between(e1, e2) = 0                            if UTC intervals overlap
                       ceil(positive UTC gap in days) otherwise

TemporalProximity(e1, e2) = 1 / (1 + days_between(e1, e2))
~~~

The file-level raw temporal value is the maximum proximity across episode pairs;
it is 0 for fewer than two episodes. D_f is normalized distinct-neighbor
degree. K_f is normalized weighted degree, the sum of combined co-change
weights. With O_f as normalized raw temporal proximity:

~~~text
BottleneckScore(f) = .35*T_f + .15*A_f + .20*O_f + .20*D_f + .10*K_f
~~~

This describes parallel-development bottleneck or pressure; it does not prove a
merge conflict occurred.

For OCP pressure, E_f is the sum of commits after the first in each task
episode touching f, but only if at least two task episodes touch it. Otherwise
it is zero. N_f is 1 if the normalized file stem has one reported bounded
token, otherwise 0. The default tokens are:

~~~text
dispatcher, registry, handler, loader, session, options, configuration,
command, diagnostic, mapper, dto, model, service, orchestrator
~~~

~~~text
OcpPressureScore(f) = .40*T_f + .25*K_f + .25*normalized(E_f) + .10*N_f
~~~

The matched token is reported and provides at most 10% of the default score. It
is heuristic evidence, not semantic proof. Reports say OCP pressure or likely
OCP violation, never OCP violation proven without separate direct evidence.

## Ranking and recommendations

File findings sort by:

1. descending score;
2. descending task spread;
3. descending churn;
4. descending commit count;
5. ascending normalized logical path.

Pairs sort by descending combined weight, commit weight, task weight, then
ascending first and second paths. Clusters sort by descending maximum edge,
descending aggregate edge weight, then ascending first member path.

Recommendations are investigations with source findings, component values,
effective thresholds, and caveats:

| Evidence | Candidate investigation |
| --- | --- |
| High OCP pressure plus role hint | Extract an extension point. |
| High co-change cluster | Revisit a module or contract boundary. |
| High bottleneck score | Split orchestration from feature-specific behavior. |
| High test-only hotspot | Improve fixture/helper architecture. |

Candidate thresholds belong to #237's schema-backed model. When nothing
qualifies, the deterministic candidate collection is empty; the tool does not
fabricate recommendations.

## Report semantics and limits

Markdown contains the analyzed range/effective configuration, hotspots,
co-change pairs/clusters, bottlenecks, OCP pressure, candidates, and limitations.
Canonical JSON contains metadata/input refs, effective configuration
identity/summary, categories, hotspot components, co-change summary, bottleneck
evidence, OCP-pressure evidence, and candidates. Arrays use the ordering above;
fields follow the documented schema order. Generated timestamps and environment
display data do not alter canonical result identity.

The Git/history core remains independent of optional .NET/Roslyn enrichment.
Later enrichment may attach project, namespace, or type facts, but parse/mapping
failure never drops, changes, or reorders the file-level finding.

Every report must make these limits clear:

- churn is not complexity;
- incomplete task/author references can understate components;
- non-production categories can dominate raw volume;
- co-change does not prove module ownership;
- role/name hints are bounded heuristics;
- people decide whether evidence warrants a refactor.

## Ownership and non-goals

- #236 implements deterministic Git ingestion and the existing CLI command family.
- #237 owns normal policy schema/configuration and path classification.
- #238/#239 implement hotspots and co-change independently.
- #240/#241 consume that evidence for bottleneck and OCP pressure.
- #242 adds optional .NET enrichment; #243 renders stable reports and candidates.

This document does not implement the analyzer, prove a formal design-law
violation, require LLM conclusions, create a separate product/configuration
authority, or freeze future scoring changes without reviewed specification work.

