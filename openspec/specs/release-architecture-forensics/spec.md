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

A logical path SHALL be repository-relative, use `/` separators, and represent
one unambiguous rename chain as one file identity. Commit file entries SHALL
retain status, additions, and deletions. Churn for file `f` in range `r` SHALL
be `Σ(additions(f, commit) + deletions(f, commit))` over its logical identity.
Churn is change volume, not a complexity measure.

Every logical path SHALL be classified as exactly one of `production`, `tests`,
`docs`, `generated`, `build_ci`, `samples_examples`, or `unknown`. The
schema-backed, bounded matcher and override syntax belong to the normal policy
model owned by #237; this capability SHALL NOT define a second configuration
authority. `unknown` paths SHALL remain visible and shall not be silently
dropped.

#### Scenario: Rename-chain normalization
- **WHEN** Git evidence unambiguously identifies a file rename within the analyzed range
- **THEN** its earlier and later paths contribute to one logical-file identity rather than independent scores

#### Scenario: Unknown path category
- **WHEN** a logical path matches no effective path category rule
- **THEN** the result records the path as `unknown` and preserves it in applicable evidence output

#### Scenario: Non-production changes
- **WHEN** a docs, generated, build/CI, test, or sample path has high churn
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

Each run SHALL have one validated effective scoring configuration. The initial
default profiles are: hotspot `(commit .30, churn .25, task .25, author .10,
temporal .10)`; bottleneck `(task .35, author .15, temporal .20, degree .20,
centrality .10)`; OCP pressure `(task .40, centrality .25, repeated_episode_edit
.25, role_hint .10)`; and combined co-change `(commit .75, task .25)`. Later
configuration is schema-backed and validated by #237; a disabled component is
an explicit effective-configuration decision, not a runtime response to absent
evidence.

#### Scenario: All-zero normalization
- **WHEN** every file in an analyzed population has zero churn, zero task spread, or another zero-valued component
- **THEN** every normalized value for that component is finite `0`

#### Scenario: No task references
- **WHEN** no commit in the range yields a task/issue reference
- **THEN** task-dependent raw components are `0`, configured task weights remain unchanged, and the report records that limitation

#### Scenario: Intentional component disablement
- **WHEN** a validated effective configuration disables a score component
- **THEN** the component is omitted only according to that configuration and the effective weights remain explicit in result metadata

### Requirement: Deterministic hotspot and co-change evidence
For every logical file `f`, the system SHALL compute:

```text
C_f = normalized(commit_count(f))
H_f = normalized_log(churn(f))
T_f = normalized(distinct_task_references(f))
A_f = normalized(distinct_normalized_authors(f))
R_f = normalized(temporal_span_seconds(f))

HotspotScore(f) = .30*C_f + .25*H_f + .25*T_f + .10*A_f + .10*R_f
```

`temporal_span_seconds(f)` SHALL be the non-negative difference between the
latest and earliest UTC Unix commit timestamps touching `f`; a one-commit file
has span `0`. This is a persistent-edit signal, not a wall-clock recency value.
Each finding SHALL retain its raw metrics and normalized components.

The co-change graph SHALL be weighted, undirected, and contain one vertex per
logical file in the range. For distinct logical files `a` and `b`:

```text
CommitCoChange(a,b) = count(commits containing both a and b)
TaskCoChange(a,b)   = count(distinct tasks whose episodes contain both a and b)
CombinedCoChange(a,b) = .75*normalized(CommitCoChange)
                        + .25*normalized(TaskCoChange)
```

Absent task evidence contributes zero. An edge pair SHALL be stored in
ascending logical-path order. A co-change cluster SHALL be a connected component
of edges meeting an explicitly configured significance threshold; when no such
threshold is effective, the result SHALL emit pair-level evidence and an empty
cluster list rather than infer a cluster from an arbitrary cutoff.

#### Scenario: Stable tied hotspot ranking
- **WHEN** two production files have equal hotspot scores
- **THEN** they are ordered by descending task spread, descending churn, descending commit count, then ascending normalized logical path

#### Scenario: Co-change without tasks
- **WHEN** two files change together in commits but no task references are extracted
- **THEN** their commit co-change weight is retained, task co-change is zero, and combined co-change uses the fixed effective weights without renormalization

#### Scenario: Stable graph input order
- **WHEN** equivalent commit/file evidence is enumerated in different input orders
- **THEN** vertices, ordered edge pairs, weights, rankings, and any thresholded clusters are identical

### Requirement: Deterministic bottleneck and OCP-pressure evidence
A task episode SHALL be the commits for one extracted task/issue reference.
For a file and two distinct task episodes, `days_between` SHALL be zero when
their UTC intervals overlap; otherwise it SHALL be the ceiling of the positive
UTC interval gap in days. `TemporalProximity(e1,e2) = 1 / (1 + days_between)`.
The raw temporal-overlap value for a file is the maximum proximity across its
distinct task-episode pairs, or `0` when fewer than two exist.

For bottlenecks, `D_f` SHALL be normalized distinct-neighbor degree and `K_f`
shall be normalized weighted degree (the sum of combined co-change weights).
With `O_f` as normalized raw temporal proximity, the score SHALL be:

```text
BottleneckScore(f) = .35*T_f + .15*A_f + .20*O_f + .20*D_f + .10*K_f
```

For OCP pressure, `E_f` SHALL be the sum of additional commits after the first
commit in each task episode touching `f`, provided `f` has at least two distinct
task episodes; otherwise it is zero. `N_f` SHALL be `1` only when the normalized
file stem matches a bounded, reported default role/name token: `dispatcher`,
`registry`, `handler`, `loader`, `session`, `options`, `configuration`,
`command`, `diagnostic`, `mapper`, `dto`, `model`, `service`, or `orchestrator`.
It is `0` otherwise. The score SHALL be:

```text
OcpPressureScore(f) = .40*T_f + .25*K_f + .25*normalized(E_f) + .10*N_f
```

Reports SHALL call these results `parallel-development bottleneck` or
`parallel-development pressure`, and `OCP pressure` or `likely OCP violation`.
They SHALL NOT claim that a merge conflict occurred or that an OCP violation is
proven unless separate direct evidence establishes that fact.

#### Scenario: One task episode
- **WHEN** a file is touched by commits linked to only one task episode
- **THEN** its temporal-proximity and repeated-episode-edit raw values are zero

#### Scenario: Overlapping task episodes
- **WHEN** two distinct task episodes touching a file overlap in UTC time
- **THEN** their temporal proximity is `1` and the finding retains the episode evidence that produced it

#### Scenario: Role hint limitation
- **WHEN** an OCP-pressure finding receives role/name-hint evidence
- **THEN** the report identifies the matched token and describes it as bounded heuristic evidence rather than semantic proof

### Requirement: Stable rankings, findings, and refactoring investigations
Every ranked file finding SHALL use descending score, then descending task
spread, descending churn, descending commit count, and ascending normalized
logical path as its stable tie-break sequence. Co-change pair rankings SHALL
use descending combined weight, descending commit weight, descending task
weight, then ascending first and second logical paths. Cluster order SHALL use
descending maximum edge weight, descending aggregate edge weight, then
ascending first member path.

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
- **THEN** ascending normalized logical path provides the final deterministic discriminator

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
identity/summary, path categories, hotspot components, co-change summary,
bottleneck evidence, OCP-pressure evidence, and refactoring candidates.

Canonical JSON arrays SHALL use the ordering defined by this capability and
fields SHALL be serialized in the documented schema order. Generated timestamps
and environment-specific display values SHALL be excluded from canonical result
identity. The Git-only core SHALL remain independent of optional .NET/Roslyn
enrichment; failed or unavailable enrichment SHALL preserve the file-level
finding unchanged.

The report SHALL state that churn is not complexity; high co-change is a
coupling/coordination signal rather than proof of module ownership; task and
author evidence may be incomplete; non-production categories can dominate raw
volume; role hints are bounded heuristics; and recommendations require human
review. Canonical scoring and recommendations SHALL not require LLM or other
stochastic inference.

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
reference that explains the product boundary, canonical input model, entities,
formulas, fixed initial profiles, ranking, report semantics, configuration
ownership, and interpretation limits in terms consistent with this capability.
The internal documentation index SHALL link to that reference. It SHALL not be
added to public MkDocs navigation until an implementation change establishes a
user-facing product contract.

#### Scenario: Contributor discovers theory
- **WHEN** a contributor opens the internal documentation index
- **THEN** it links to the Release Architecture Forensics theory reference

#### Scenario: Public documentation boundary
- **WHEN** the MkDocs navigation is built before the feature is implemented
- **THEN** it does not advertise Release Architecture Forensics as a shipped user-facing command or policy feature
