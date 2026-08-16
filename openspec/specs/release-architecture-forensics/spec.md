# release-architecture-forensics Specification

## Purpose

Define the deterministic Git-range evidence, scoring, finding, recommendation,
and report semantics that govern Release Architecture Forensics implementation.

## Requirements

### Requirement: Explicit deterministic analysis identity
Release Architecture Forensics SHALL analyze an explicit Git range whose
`from` ref is exclusive and whose `to` ref is inclusive. Both refs SHALL be
resolved before analysis; a missing, ambiguous, or unresolvable ref SHALL fail
closed with a diagnostic rather than selecting a default branch or range.

The canonical analysis identity SHALL contain the authored refs, their resolved
commit IDs, the effective `history_analysis` configuration identity, and the
tool version. It SHALL use normalized repository-relative logical paths and
shall not contain an absolute checkout root, local timezone, generated-at
timestamp, or other environment-dependent presentation data. Uncommitted
working-tree state SHALL not alter Git-only evidence.

Commit records SHALL use a stable ascending order of committer UTC timestamp
followed by ordinal commit SHA. A normalized author identity SHALL be the
trimmed, invariant-lowercase email when one is present; otherwise it SHALL be
the trimmed, invariant-lowercase author name; an absent value SHALL be the
literal `unknown`. Task/issue references SHALL be extracted, deduplicated, and
ordered deterministically under the effective configuration.

#### Scenario: Missing explicit ref
- **WHEN** an analysis request names a `from` or `to` ref that cannot be resolved
- **THEN** the analysis fails with a non-success result and does not substitute another ref

#### Scenario: Equivalent execution environments
- **WHEN** the same repository objects, resolved refs, effective configuration, and tool version are analyzed from two checkout roots or timezones
- **THEN** their canonical results have identical identities, logical paths, findings, and canonical JSON

#### Scenario: Empty range
- **WHEN** an explicit range resolves to no commits
- **THEN** the result succeeds with an explicit empty-range summary and deterministic empty/zero findings rather than an undefined result

### Requirement: Canonical evidence entities and path classification
The Git/history core SHALL model an analyzed range, commits, normalized authors,
repository-relative logical file paths and rename chains, extracted task/issue
references and task episodes, path categories, file hotspots, co-change edges
and clusters, parallel-development bottlenecks, OCP-pressure points, and
evidence-backed refactoring candidates.

A logical file SHALL represent one unambiguous linear rename chain. Its
canonical logical path SHALL be the normalized repository-relative path at the
last in-range occurrence of that chain, including the deleted path when the last
occurrence is a deletion. Earlier paths SHALL remain ordered aliases/evidence but
SHALL NOT create independent file scores. A copy, split, merge, or otherwise
ambiguous rename relation SHALL NOT be collapsed: each path remains a separate
logical-file identity unless Git evidence establishes one unambiguous chain.

Commit file entries SHALL retain status, additions, deletions, observed path,
and logical-file identity. Churn for file `f` in range `r` SHALL be
`Σ(additions(f, commit) + deletions(f, commit))` over its logical identity.
Churn is change volume, not a complexity measure.

Every logical file SHALL have exactly one primary category, derived by applying
the effective #237 classifier to its canonical logical path: `production`,
`tests`, `docs`, `generated`, `build_ci`, `samples_examples`, or `unknown`.
Alias paths and their classifications MAY be retained as evidence, but they
SHALL NOT override the primary category or ranking path. The schema-backed,
bounded matcher, ignore model, category overrides, and thresholds belong to the
normal policy model owned by #237; this capability SHALL NOT define a second
configuration authority. `unknown` paths SHALL remain visible and shall not be
silently dropped.

#### Scenario: Rename-chain normalization
- **WHEN** Git evidence unambiguously identifies `src/Old.cs` renamed to `tests/New.cs` within the analyzed range
- **THEN** both observations contribute to one logical file whose canonical path is `tests/New.cs` and whose primary category is derived from that canonical path

#### Scenario: Ambiguous rename evidence
- **WHEN** Git evidence represents a copy, split, merge, or otherwise ambiguous path relationship
- **THEN** the analyzer does not invent one logical identity and keeps the affected paths separate

#### Scenario: Unknown path category
- **WHEN** a canonical logical path matches no effective path category rule
- **THEN** the result records the logical file as `unknown` and preserves it in applicable evidence output

#### Scenario: Non-production changes
- **WHEN** a docs, generated, build/CI, test, or sample logical file has high churn
- **THEN** its category remains available so reporting policy can distinguish it from a primary production signal

### Requirement: Total normalization and effective scoring configuration
For every non-negative component population, the normalizer SHALL be:

```text
normalized(x) = 0                              when max(population) = 0
                x / max(population)            otherwise

normalized_log(x) = 0                          when max(population) = 0
                    log(1 + x) / log(1 + max)  otherwise
```

All-zero and empty populations SHALL return finite `0` values; they SHALL NOT
produce NaN, Infinity, exceptions, or implementation-specific fallbacks. A
missing optional evidence family, including absent task references, contributes
raw `0` to each dependent component. The system SHALL NOT renormalize remaining
component weights because an evidence family is absent.

Ignore rules owned by #237 are analysis filters: ignored logical files SHALL be
removed before score populations and graph evidence are built. Presentation-only
suppression SHALL NOT change canonical scores. File-level metrics, including
hotspot, bottleneck, and OCP components, SHALL be normalized against retained
logical files in the same primary path category. Co-change edge components SHALL
be normalized against retained edges with the same unordered endpoint-category
pair. This category-cohort rule prevents generated/docs/test/build volume from
setting the maximum for production findings while keeping each category's
results internally comparable.

Each run SHALL have one validated effective scoring configuration. The initial
default profiles are: hotspot `(commit .30, churn .25, task .25, author .10,
temporal .10)`; bottleneck `(task .35, author .15, temporal .20, degree .20,
centrality .10)`; OCP pressure `(task .40, centrality .25, repeated_episode_edit
.25, role_hint .10)`; and combined co-change `(commit .75, task .25)`. #237 MAY
make those weights configurable, but every score SHALL consume the effective
validated weights for that run. A disabled component is an explicit effective-
configuration decision, represented by its effective weight, not a runtime
response to absent evidence. No runtime weight renormalization is allowed.

#### Scenario: All-zero normalization
- **WHEN** every retained entity in a component's category cohort has value zero
- **THEN** every normalized value for that component is finite `0`

#### Scenario: Category isolation
- **WHEN** a retained generated file has much higher churn than every retained production file
- **THEN** it does not change the production churn normalization maximum because generated and production files use separate category cohorts

#### Scenario: Ignored file
- **WHEN** the effective policy ignores a logical file
- **THEN** that file does not participate in normalization populations, graph vertices, graph edges, findings, or candidates

#### Scenario: No task references
- **WHEN** no commit in the range yields a task/issue reference
- **THEN** task-dependent raw components are `0`, configured task weights remain unchanged, and the report records that limitation

#### Scenario: Intentional component disablement
- **WHEN** a validated effective configuration disables a score component
- **THEN** the component follows its explicit effective weight and the remaining weights are not changed implicitly at runtime

### Requirement: Deterministic hotspot and co-change evidence
For every retained logical file `f`, using effective hotspot weights
`w_c,w_h,w_t,w_a,w_r`, the system SHALL compute:

```text
C_f = normalized(commit_count(f))
H_f = normalized_log(churn(f))
T_f = normalized(distinct_task_references(f))
A_f = normalized(distinct_normalized_authors(f))
R_f = normalized(temporal_span_seconds(f))

HotspotScore(f) = w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f
```

The default hotspot weights are `.30,.25,.25,.10,.10` respectively.
`temporal_span_seconds(f)` SHALL be the non-negative difference between the
latest and earliest UTC Unix commit timestamps touching `f`; a one-commit file
has span `0`. This is a persistent-edit signal, not a wall-clock recency value.
Each finding SHALL retain its raw metrics, normalized components, and effective
weights.

The co-change graph SHALL be weighted, undirected, and contain one vertex per
retained logical file in the range. For distinct logical files `a` and `b`,
using effective co-change weights `alpha,beta`:

```text
CommitCoChange(a,b) = count(commits containing both a and b)
TaskCoChange(a,b)   = count(distinct tasks whose episodes contain both a and b)

CombinedCoChange(a,b) = alpha*normalized(CommitCoChange)
                        + beta*normalized(TaskCoChange)
```

The default co-change weights are `alpha=.75` and `beta=.25`. Absent task
evidence contributes zero without changing those effective weights. An edge
pair SHALL be stored in ascending canonical logical-path order. A co-change
cluster SHALL be a connected component of edges meeting an explicitly configured
significance threshold; when no such threshold is effective, the result SHALL
emit pair-level evidence and an empty cluster list rather than infer a cluster
from an arbitrary cutoff.

#### Scenario: Stable tied hotspot ranking
- **WHEN** two logical files in the same primary category have equal hotspot scores
- **THEN** they are ordered by descending task spread, descending churn, descending commit count, then ascending canonical logical path

#### Scenario: Configured hotspot weights
- **WHEN** the effective configuration changes an enabled hotspot weight from its default
- **THEN** the score uses the configured weight and reports that effective value rather than silently using the default literal

#### Scenario: Co-change without tasks
- **WHEN** two files change together in commits but no task references are extracted
- **THEN** their commit co-change weight is retained, task co-change is zero, and combined co-change uses the effective weights without renormalization

#### Scenario: Stable graph input order
- **WHEN** equivalent commit/file evidence is enumerated in different input orders
- **THEN** vertices, ordered edge pairs, weights, rankings, and any thresholded clusters are identical

### Requirement: Independent task evidence for parallel-development signals
A task episode SHALL be the commits linked to one extracted task/issue reference.
A single commit MAY reference multiple tasks and MAY contribute ordinary task
spread and task-level co-change evidence to each referenced task; that alone
SHALL NOT prove that those task references represent independent workstreams.

For a logical file `f`, two task references `x` and `y` form an independent task
pair only when both sides have pair-exclusive evidence: at least one commit
touching `f` references `x` but not `y`, and at least one commit touching `f`
references `y` but not `x`. Commits that reference both `x` and `y` SHALL NOT be
used to establish independence, temporal overlap/proximity between that pair, or
repeated-episode-edit evidence for that pair.

`IndependentTaskSpread(f)` SHALL be the number of task references that
participate in at least one independent pair for `f`. For an independent pair,
each pair-side interval SHALL be built from that side's pair-exclusive commits.
`days_between` SHALL be zero when those pair-exclusive UTC intervals overlap;
otherwise it SHALL be the ceiling of the positive UTC interval gap in days.
`TemporalProximity(e1,e2) = 1 / (1 + days_between)`. The raw temporal value for
a file is the maximum proximity across independent pairs, or `0` when no
independent pair exists.

#### Scenario: One multi-reference commit
- **WHEN** the only commit touching a file references both `#101` and `#102`
- **THEN** ordinary task spread may record both references, but independent task spread, temporal proximity, and repeated-episode-edit evidence are all `0`

#### Scenario: Independent task episodes
- **WHEN** `#101` and `#102` each have at least one file-touching commit that does not reference the other
- **THEN** they form an independent pair and temporal proximity is calculated from their pair-exclusive commit intervals

### Requirement: Deterministic bottleneck and OCP-pressure evidence
For bottlenecks, `T_f` SHALL be normalized `IndependentTaskSpread(f)`, `A_f`
shall be normalized author spread, `O_f` SHALL be normalized independent-task
temporal proximity, `D_f` SHALL be normalized distinct-neighbor degree, and
`K_f` SHALL be normalized weighted degree (the sum of combined co-change
weights). Using effective bottleneck weights `b_t,b_a,b_o,b_d,b_c`:

```text
BottleneckScore(f) = b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f
```

The default bottleneck weights are `.35,.15,.20,.20,.10` respectively.

For OCP pressure, `T_f` SHALL also use normalized independent task spread.
`E_f` SHALL count repeated pair-exclusive editing across task references that
participate in at least one independent pair: for each such reference, count
pair-exclusive commits touching `f` after the first unique qualifying commit,
with a commit counted at most once per task reference. If no independent pair
exists, `E_f` is zero.

For role hints, the normalized file stem SHALL be the canonical file name without
its final extension, tokenized deterministically by splitting on non-alphanumeric
characters, lower-to-upper camel/Pascal transitions, acronym-to-word boundaries,
and letter/digit boundaries; tokens are invariant-lowercase. Matching SHALL use
exact token equality only: no substring, glob, or regex matching. The default
role/name tokens are `dispatcher`, `registry`, `handler`, `loader`, `session`,
`options`, `configuration`, `command`, `diagnostic`, `mapper`, `dto`, `model`,
`service`, and `orchestrator`. `N_f` SHALL be `1` when at least one token matches
and `0` otherwise. All matched tokens SHALL be reported in ascending ordinal
order.

Using effective OCP weights `o_t,o_c,o_r,o_n`:

```text
OcpPressureScore(f) = o_t*T_f + o_c*K_f + o_r*normalized(E_f) + o_n*N_f
```

The default OCP weights are `.40,.25,.25,.10` respectively. Reports SHALL call
these results `parallel-development bottleneck` or `parallel-development
pressure`, and `OCP pressure` or `likely OCP violation`. They SHALL NOT claim
that a merge conflict occurred or that an OCP violation is proven unless
separate direct evidence establishes that fact.

#### Scenario: One task episode
- **WHEN** a file is touched by commits linked to only one task reference
- **THEN** its independent task spread, temporal-proximity, and repeated-episode-edit raw values are zero

#### Scenario: Overlapping independent task episodes
- **WHEN** two independent task references have pair-exclusive UTC intervals that overlap
- **THEN** their temporal proximity is `1` and the finding retains the pair-exclusive episode evidence that produced it

#### Scenario: Role hint tokenization
- **WHEN** canonical stems are `OrderService`, `DiagnosticMapper`, and `ViewModel`
- **THEN** tokenization yields exact role-token matches `service`, `diagnostic`/`mapper`, and `model` respectively, while a mere substring inside an unsplit token does not match

#### Scenario: Effective OCP weights
- **WHEN** the effective configuration changes an OCP or bottleneck weight
- **THEN** the corresponding score consumes and reports that effective weight rather than a hard-coded default

### Requirement: Stable rankings, findings, and refactoring investigations
Every ranked file finding SHALL use descending score, then descending ordinary
task spread, descending churn, descending commit count, and ascending canonical
logical path as its stable tie-break sequence. Co-change pair rankings SHALL use
descending combined weight, descending commit weight, descending task weight,
then ascending first and second canonical logical paths. Cluster order SHALL use
descending maximum edge weight, descending aggregate edge weight, then ascending
first member canonical path.

Refactoring candidates SHALL be evidence-derived investigations, not automatic
redesign decisions. Each candidate SHALL carry its source finding identifiers,
component/evidence values, effective thresholds, and interpretation caveat. The
canonical mapping is: high OCP pressure plus role-hint evidence suggests
investigating extension-point extraction; a high co-change cluster suggests
investigating a module or contract boundary; a high bottleneck suggests
investigating orchestration or feature separation; and a high test-only hotspot
suggests investigating fixture/helper architecture. Candidate emission
thresholds are schema-backed configuration owned by #237.

#### Scenario: Equivalent-score total order
- **WHEN** all numeric rank dimensions for two file findings are equal
- **THEN** ascending canonical logical path provides the final deterministic discriminator

#### Scenario: Evidence-backed candidate
- **WHEN** a finding qualifies for a refactoring candidate
- **THEN** the candidate names the source evidence and caveat and does not present a refactoring as an automatic conclusion

#### Scenario: No qualifying candidates
- **WHEN** no finding crosses effective candidate thresholds
- **THEN** the report emits an empty deterministic candidate collection and does not fabricate recommendations

### Requirement: Deterministic report semantics and interpretation limits
The reporting layer SHALL produce stable markdown and canonical JSON from the
same file-level evidence. Markdown SHALL contain analyzed range and effective
configuration summary, top hotspots, co-change pairs/clusters, bottlenecks, OCP
pressure, refactoring candidates, and limitations/interpretation notes.
Canonical JSON SHALL contain metadata/input refs, effective configuration
identity/summary, primary path categories, canonical paths and aliases, hotspot
components, co-change summary, independent-task bottleneck evidence,
OCP-pressure evidence, effective score weights, and refactoring candidates.

Canonical JSON arrays SHALL use the ordering defined by this capability and
fields SHALL be serialized in the documented schema order. Generated timestamps
and environment-specific display values SHALL be excluded from canonical result
identity. The Git-only core SHALL remain independent of optional .NET/Roslyn
enrichment; failed or unavailable enrichment SHALL preserve the file-level
finding unchanged.

The report SHALL state that churn is not complexity; high co-change is a
coupling/coordination signal rather than proof of module ownership; task and
author evidence may be incomplete; multi-reference commits are not independent
workstream proof; non-production categories can dominate their own raw volume;
role hints are bounded heuristics; and recommendations require human review.
Canonical scoring and recommendations SHALL not require LLM or other stochastic
inference.

#### Scenario: Deterministic report serialization
- **WHEN** identical canonical evidence is rendered twice
- **THEN** markdown ordering and canonical JSON content are identical without generated-time or checkout-root differences

#### Scenario: Unavailable .NET enrichment
- **WHEN** optional C# enrichment cannot parse or map a changed file
- **THEN** the report retains the original file-level finding and records enrichment as unavailable rather than dropping or changing the score

#### Scenario: Interpretation notes
- **WHEN** a report contains a hotspot, co-change, bottleneck, OCP-pressure finding, or candidate
- **THEN** it includes the applicable evidence and limitation language required to prevent it being interpreted as formal design-law proof

### Requirement: Contributor theory reference
The repository SHALL contain a contributor-facing Release Architecture Forensics
reference that explains the product boundary, canonical input model, logical-file
identity, category-cohort normalization, task independence, formulas, default and
effective profiles, role-token matching, ranking, report semantics,
configuration ownership, and interpretation limits in terms consistent with
this capability. The internal documentation index SHALL link to that reference.
It SHALL not be added to public MkDocs navigation until an implementation change
establishes a user-facing product contract.

#### Scenario: Contributor discovers theory
- **WHEN** a contributor opens the internal documentation index
- **THEN** it links to the Release Architecture Forensics theory reference

#### Scenario: Public documentation boundary
- **WHEN** the MkDocs navigation is built before the feature is implemented
- **THEN** it does not advertise Release Architecture Forensics as a shipped user-facing command or policy feature
