## ADDED Requirements

### Requirement: Explicit deterministic analysis identity
Release Architecture Forensics SHALL analyze an explicit Git range whose
`from` ref is exclusive and whose `to` ref is inclusive. Both refs SHALL resolve
before analysis; missing, ambiguous, or unresolvable refs SHALL fail closed.
Canonical identity SHALL contain authored refs, resolved commit IDs, effective
`history_analysis` configuration identity, and tool version, while excluding
absolute checkout roots, generated timestamps, timezone, locale, and other
environment-dependent presentation data. Commit records SHALL sort ascending by
committer UTC timestamp then ordinal SHA. Author identity SHALL use trimmed
invariant-lowercase email, else name, else `unknown`. Task references SHALL be
extracted, deduplicated, and ordered deterministically.

#### Scenario: Equivalent execution environments
- **WHEN** identical repository objects, refs, configuration, and tool version are analyzed from different checkout roots or timezones
- **THEN** canonical identity, evidence, findings, and JSON are identical

#### Scenario: Empty range
- **WHEN** an explicit range contains no commits
- **THEN** analysis succeeds with deterministic empty/zero evidence

### Requirement: Canonical logical-file identity and path classification
A logical file SHALL represent one unambiguous linear rename chain. Its canonical
logical path SHALL be the normalized repository-relative path at the last
in-range occurrence, including the deleted path when the final occurrence is a
deletion. Earlier paths SHALL remain ordered aliases/evidence and SHALL NOT
receive independent file scores. Copies, splits, merges, and otherwise ambiguous
rename relationships SHALL remain separate logical identities unless Git
evidence establishes one unambiguous chain.

Each logical file SHALL have exactly one primary category derived by applying the
effective #237 classifier to its canonical logical path: `production`, `tests`,
`docs`, `generated`, `build_ci`, `samples_examples`, or `unknown`. Alias
classifications MAY remain evidence but SHALL NOT replace the primary category
or ranking path. Commit-file entries SHALL retain status, additions, deletions,
observed path, and logical identity. Churn SHALL be the sum of additions plus
deletions over that identity and SHALL be described as volume, not complexity.

#### Scenario: Rename across categories
- **WHEN** `src/Old.cs` is unambiguously renamed to `tests/New.cs` in range
- **THEN** one logical file is scored using canonical path `tests/New.cs` and the primary category derived from that path

#### Scenario: Ambiguous rename evidence
- **WHEN** Git evidence represents a copy, split, merge, or otherwise ambiguous relationship
- **THEN** the analyzer keeps the affected paths as separate logical identities

### Requirement: Total normalization and deterministic populations
For every non-negative component population:

```text
normalized(x) = 0                              when max(population) = 0
                x / max(population)            otherwise

normalized_log(x) = 0                          when max(population) = 0
                    log(1 + x) / log(1 + max)  otherwise
```

Empty/all-zero populations SHALL produce finite `0`; missing optional evidence
SHALL contribute raw `0`; runtime weight renormalization SHALL NOT occur. #237
ignore rules are analysis filters, so ignored logical files SHALL be removed
before normalization and graph construction. Presentation-only suppression
SHALL NOT alter canonical scores.

File-level hotspot, bottleneck, and OCP components SHALL normalize against
retained logical files in the same primary category. Co-change edge components
SHALL normalize against retained edges with the same unordered endpoint-category
pair. Thus non-production volume SHALL NOT set production normalization maxima.

#### Scenario: Category isolation
- **WHEN** a generated file has much higher churn than retained production files
- **THEN** it does not alter the production churn normalization maximum

#### Scenario: Ignored file
- **WHEN** policy ignores a logical file
- **THEN** it does not participate in normalization, graph evidence, findings, or candidates

### Requirement: Effective scoring configuration
Each run SHALL use one validated effective scoring configuration. Defaults are:
hotspot `(commit .30, churn .25, task .25, author .10, temporal .10)`;
bottleneck `(independent task .35, author .15, temporal .20, degree .20,
centrality .10)`; OCP pressure `(independent task .40, centrality .25,
repeated_episode_edit .25, role_hint .10)`; co-change `(commit .75, task .25)`.
#237 MAY make weights configurable. Every formula SHALL consume the effective
validated weights; a disabled component SHALL be explicit, and missing evidence
SHALL NOT change other weights at runtime.

#### Scenario: Configured weight
- **WHEN** an effective profile changes a default weight
- **THEN** scoring consumes and reports the effective value rather than the default literal

### Requirement: Deterministic hotspot and co-change evidence
For retained logical file `f`, using effective hotspot weights:

```text
C_f = normalized(commit_count(f))
H_f = normalized_log(churn(f))
T_f = normalized(distinct_task_references(f))
A_f = normalized(distinct_normalized_authors(f))
R_f = normalized(temporal_span_seconds(f))

HotspotScore(f) = w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f
```

`temporal_span_seconds` SHALL be latest minus earliest UTC Unix timestamp for the
file, with one-commit span `0`. Findings SHALL retain raw metrics, normalized
components, and effective weights.

For retained logical files `a,b`, using effective co-change weights:

```text
CommitCoChange(a,b) = count(commits containing both a and b)
TaskCoChange(a,b)   = count(distinct tasks whose episodes contain both a and b)
CombinedCoChange(a,b) = alpha*normalized(CommitCoChange)
                        + beta*normalized(TaskCoChange)
```

Pair paths SHALL use ascending canonical logical-path order. Missing task
evidence SHALL be zero without weight renormalization. Clusters SHALL be emitted
only from edges crossing an explicit effective threshold; without one, pair
evidence remains and the cluster list is empty.

#### Scenario: Stable tied hotspot ranking
- **WHEN** same-category files have equal hotspot scores
- **THEN** ranking uses descending ordinary task spread, churn, commit count, then ascending canonical path

#### Scenario: Co-change without tasks
- **WHEN** files co-change but no task refs are extracted
- **THEN** commit evidence remains, task evidence is zero, and effective weights remain unchanged

### Requirement: Independent task evidence for parallel-development signals
A task episode SHALL be commits linked to one extracted reference. A commit MAY
reference multiple tasks and contribute ordinary task spread/task co-change to
each, but that alone SHALL NOT establish independent workstreams.

For file `f`, refs `x,y` form an independent pair only when each side has at
least one pair-exclusive commit touching `f`: one references `x` but not `y`,
and one references `y` but not `x`. Shared-reference commits SHALL NOT establish
independence, temporal overlap/proximity, or repeated-episode-edit evidence for
that pair. `IndependentTaskSpread(f)` SHALL count references participating in at
least one independent pair.

Temporal intervals SHALL use pair-exclusive commits. Their gap is zero when UTC
intervals overlap, otherwise the ceiling of the positive gap in days, and
`TemporalProximity = 1/(1+days_between)`. File raw temporal value SHALL be the
maximum across independent pairs, or zero when none exist.

#### Scenario: One multi-reference commit
- **WHEN** the only file-touching commit references both `#101` and `#102`
- **THEN** ordinary task spread may contain both refs but independent task spread, temporal proximity, and repeated-episode-edit evidence are zero

#### Scenario: Independent pair
- **WHEN** each of two refs has file-touching pair-exclusive commit evidence
- **THEN** temporal proximity is calculated from those pair-exclusive intervals

### Requirement: Deterministic bottleneck and OCP-pressure evidence
Bottleneck `T_f` SHALL be normalized `IndependentTaskSpread`, `A_f` normalized
author spread, `O_f` normalized independent-task temporal proximity, `D_f`
normalized distinct-neighbor degree, and `K_f` normalized weighted degree.
Using effective weights:

```text
BottleneckScore(f) = b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f
```

OCP `T_f` SHALL also use independent task spread. `E_f` SHALL count repeated
pair-exclusive edits for refs participating in at least one independent pair,
counting qualifying commits after the first unique commit per task ref and no
commit more than once per task ref; with no independent pair `E_f=0`.

Role hints SHALL use the canonical filename without final extension. The stem
SHALL be tokenized by non-alphanumeric boundaries, lower-to-upper camel/Pascal
transitions, acronym-to-word boundaries, and letter/digit boundaries, then
invariant-lowercased. Matching SHALL use exact token equality only, never
substring/glob/regex matching. Default tokens are `dispatcher`, `registry`,
`handler`, `loader`, `session`, `options`, `configuration`, `command`,
`diagnostic`, `mapper`, `dto`, `model`, `service`, `orchestrator`. `N_f=1` when
one or more tokens match, otherwise zero, and all matched tokens SHALL be
reported in ascending ordinal order.

```text
OcpPressureScore(f) = o_t*T_f + o_c*K_f + o_r*normalized(E_f) + o_n*N_f
```

Reports SHALL describe parallel-development pressure/bottlenecks and OCP
pressure/likely OCP violations as heuristic evidence, never proof of an actual
merge conflict or formal OCP violation without separate direct evidence.

#### Scenario: Role tokenization
- **WHEN** stems are `OrderService`, `DiagnosticMapper`, and `ViewModel`
- **THEN** exact token matches are `service`, `diagnostic`/`mapper`, and `model`; a substring inside an unsplit token does not match

#### Scenario: One task episode
- **WHEN** only one task ref touches a file
- **THEN** independent task spread, temporal proximity, and repeated-episode-edit evidence are zero

### Requirement: Stable rankings and refactoring investigations
File findings SHALL rank by descending score, ordinary task spread, churn,
commit count, then ascending canonical logical path. Co-change pairs SHALL rank
by descending combined, commit, task weight, then ascending canonical paths.
Clusters SHALL rank by descending maximum edge, aggregate edge weight, then
ascending first-member canonical path.

Candidates SHALL be evidence-derived investigations with source finding IDs,
components/evidence, effective thresholds, and caveats. High OCP pressure plus a
role hint suggests extension-point investigation; high co-change cluster suggests
boundary investigation; high bottleneck suggests orchestration/feature split;
high test-only hotspot suggests fixture/helper architecture. No qualifying
finding SHALL produce an empty candidate collection rather than fabricated work.

#### Scenario: Equivalent-score total order
- **WHEN** numeric rank dimensions tie
- **THEN** ascending canonical logical path is the final discriminator

### Requirement: Deterministic report semantics and interpretation limits
Markdown SHALL contain range/config summary, hotspots, co-change pairs/clusters,
bottlenecks, OCP pressure, candidates, and interpretation limits. Canonical JSON
SHALL include input/config identity, canonical paths and aliases, primary
categories, raw/normalized components, effective weights, co-change evidence,
independent-task evidence, OCP evidence, and candidates with stable array and
field ordering. Generated timestamps and environment display data SHALL NOT alter
canonical identity. Optional .NET/Roslyn enrichment SHALL remain downstream and
failure SHALL NOT drop, change, or reorder file-level findings.

Reports SHALL state that churn is not complexity, co-change is not module-ownership
proof, task/author evidence may be incomplete, multi-reference commits are not
independent-work proof, role hints are bounded heuristics, and refactoring needs
human judgment. Canonical scoring/recommendations SHALL not require stochastic
inference.

#### Scenario: Deterministic rendering
- **WHEN** identical canonical evidence is rendered twice
- **THEN** markdown ordering and canonical JSON content are identical

### Requirement: Contributor theory reference
The repository SHALL contain an internal contributor reference consistent with
this capability, covering logical-file identity, category-cohort normalization,
independent-task semantics, effective scoring profiles, role-token matching,
rankings, reports, ownership, and limits. The internal docs index SHALL link it;
public MkDocs navigation SHALL not advertise the unimplemented feature.

#### Scenario: Contributor discovers theory
- **WHEN** a contributor opens the internal documentation index
- **THEN** it links to the Release Architecture Forensics theory reference
