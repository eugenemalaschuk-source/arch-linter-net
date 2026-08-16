# Release Architecture Forensics Theory

This contributor reference is the readable theory authority for Release
Architecture Forensics introduced by #234. It is owned by #235 and synchronized
with the
[OpenSpec capability](../../openspec/specs/release-architecture-forensics/spec.md).

It describes planned behavior for #236–#244, not a currently shipped command or
policy surface.

## Product boundary

Release forensics asks what architecture pressure accumulated across an explicit
Git range, which files became coordination bottlenecks, and what refactoring
investigations are justified by that evidence.

Canonical output is deterministic evidence, not proof of a design-law violation,
merge conflict, module boundary, or correct refactoring. No LLM is required for
canonical scoring or recommendations.

## Canonical analysis identity

One run is identified by:

~~~text
(repository objects, explicit from ref, explicit to ref,
 effective history_analysis policy, history-semantics profile, tool version)
~~~

Local checkout roots, locale, timezone, generated timestamps, process state, and
working-tree changes do not enter Git-only canonical identity.

## Raw commit metadata is canonical input

Commit author/message/time evidence is read from raw Git commit object bytes, not
from `git log` or a library presentation API that may silently transcode or
calendar-convert metadata.

The raw commit object is parsed as LF-delimited headers followed by the first
empty header line and raw message payload. Malformed required headers or unreadable
required commit objects fail closed. Continuation lines of unrelated headers may
be retained opaquely; they do not change direct `author ` / `committer ` matching.

### Exact `author` header parsing

Exactly one canonical `author` header is used. After the literal `author ` prefix,
parse from right to left:

~~~text
<identity-bytes> SP <timestamp-token> SP <timezone-token>
~~~

- timestamp matches `-?[0-9]+`;
- timezone matches `[+-][0-9]{4}`;
- remaining identity bytes end in `>`;
- the final `>` and last `<` before it delimit email bytes;
- bytes before that `<` are name bytes.

If this structure is not uniquely parseable, analysis fails instead of delegating
to backend identity formatting.

Canonical author identity then:

1. trims ASCII SP/HT from email bytes;
2. selects non-empty email, otherwise ASCII-SP/HT-trimmed name;
3. strict-UTF8 decodes selected bytes or fails closed;
4. trims only ASCII SP/HT;
5. lowercases ASCII `A-Z` only;
6. performs no Unicode normalization, locale casing, or full Unicode case folding;
7. uses `unknown` only when both parsed email and name are empty.

### Exact `committer` epoch-second parsing

Exactly one canonical `committer` header is required and uses the same right-to-
left suffix grammar. Its timestamp token is interpreted as an arbitrary-precision
signed base-10 **Unix epoch-second integer**. Leading zeroes and `-0` are only
spellings; canonical numeric zero is `0`.

The timezone token is structurally validated and retained as metadata, but it is
never added to or subtracted from the epoch value. Canonical commit ordering and
all temporal math compare/use the exact timestamp integer directly, then full SHA
as the commit-order tie-break.

No local date, wall-clock conversion, DST rule, floating seconds, or host
`DateTime` range participates. Even an epoch integer outside a host calendar
library's representable range remains valid canonical temporal evidence.

The optional Git `encoding` header is evidence only. It never causes canonical
transcoding of author or message bytes.

## Canonical task references

Task extraction runs on the **raw message payload** after strict UTF-8 decoding.
Invalid UTF-8 fails closed even when a Git client could transcode the payload from
a legacy `encoding` header.

Each #237 extractor maps a match to:

- stable ASCII-lowercase namespace `[a-z][a-z0-9._-]*`;
- positive ASCII-decimal identifier `[0-9]+`.

Canonical identity is structural:

~~~text
TaskKey = (namespace, positive_decimal_id)
~~~

The decimal is arbitrary precision, greater than zero, and renders without leading
zeroes. Therefore:

~~~text
#001   -> (issue, 1)
#1     -> (issue, 1)
#0     -> no default TaskKey
jira-1 -> (jira, 1)
~~~

`issue#1` and `jira#1` are different tasks. Source spelling, extractor ID, matched
text, and half-open **raw message byte span** `[start,end)` are evidence, not
identity.

Repeated matches producing the same TaskKey deduplicate. If overlapping byte
spans map to different TaskKeys, analysis fails with an extraction-ambiguity
diagnostic rather than depending on extractor order or nesting. Non-overlapping
matches may yield distinct TaskKeys.

The default extractor uses namespace `issue` and recognizes `#<positive decimal>`.
All task spread, task episodes, TaskCoChange, independent pairs, and repeated-edit
signals consume canonical TaskKeys.

## Canonical commit set

`from` is exclusive and `to` inclusive:

~~~text
Commits(from,to) = Reachable(to) \ Reachable(from)
~~~

`Reachable(r)` includes `r` and every parent-reachable commit. The formula also
applies when `from` is not an ancestor of `to`.

Commits sort by exact canonical committer epoch-second integer, then full SHA. A
one-parent commit uses the parent-tree -> commit-tree delta. A root commit uses
the empty tree. Merge commits stay in range metadata but contribute no v1 file-
derived evidence, preventing first-parent/combined-diff ambiguity and double
counting while knowingly understating merge-resolution-only edits.

## Canonical Git paths and ordering

Git paths are bytes. V1 accepts only strict UTF-8. Invalid UTF-8 fails closed
before classification, rename identity, ranking, or JSON. There is no locale/code-
page fallback or replacement decoding.

Strict decoding preserves the exact Unicode scalar sequence. NFC/NFD/NFKC/NFKD
normalization is forbidden, so canonically equivalent but scalar-distinct path
spellings remain distinct.

Canonical ordinal ordering means lexicographic Unicode scalar numeric value:

1. compare corresponding scalars numerically;
2. lower first differing scalar sorts first;
3. exact-prefix shorter sequence sorts first.

Host UTF-16 ordering, filesystem collation, locale collation, and Unicode
normalization libraries are not authoritative. Repository paths use `/`.

## Exact rename candidates and Git-DAG safety

V1 does not use ambient Git similarity heuristics. Inside one non-merge commit, a
**local exact-rename candidate** exists only when a one-to-one delete/add relation
has identical preimage/postimage blob ID and no competing same-commit source or
destination.

Similarity, copy inference, rename-with-edit, candidate thresholds, and Git client
configuration cannot create canonical candidates.

For candidate `c`, define `src(c)`, `dst(c)`, and `commit(c)`. Build an undirected
candidate-overlap graph `H`:

~~~text
Endpoints(c) = { src(c), dst(c) }
H edge(c1,c2) iff Endpoints(c1) intersects Endpoints(c2)
~~~

Connected components of `H` are the exact potential lineages. A component
canonicalizes only if **exactly one** permutation `(c1,...,ck)` uses every
candidate and:

1. every earlier candidate commit is a strict Git ancestor of every later one;
2. each `dst(ci) == src(c{i+1})`;
3. for shared path `p` between adjacent candidates, no ordinary canonical add or
   delete of `p` occurs in a non-merge commit strictly between their commits.

Rule 3 is the lifecycle guard: deleting and later recreating the same path breaks
identity even if the path spelling happens to connect two exact moves.

If no such all-candidate sequence exists, more than one exists, or a lifecycle
break exists, the component is `ambiguous_dag`. No candidate in it collapses
identity. Timestamp+SHA order never turns incomparable branches or reused path
names into a lineage.

Examples:

~~~text
A -> B -> C on descendants, no lifecycle break => one logical file, canonical C
A -> B -> A on descendants                    => canonical A, alias B
A -> {B, C} same commit                       => no competing local candidate
branch 1: A -> B
branch 2: A -> C                              => ambiguous_dag
A -> B ; ordinary delete B ; add B ; B -> C  => ambiguous_dag lifecycle break
~~~

For an accepted unique lineage, canonical path is the last in-range occurrence
(including final deletion); historical non-canonical paths are distinct aliases.
Aliases sort by first canonical occurrence, then scalar-value path order.

For an `ambiguous_dag` lineage, local candidate evidence may be reported, but the
delete/add entries remain separate file events and do **not** receive exact-rename
zero-churn treatment.

## Canonical file events and line churn

There is one canonical file event per logical file per canonical file-evidence
commit.

An accepted exact rename collapses its delete/add pair into one event:

~~~text
canonical_additions = 0
canonical_deletions = 0
canonical_churn     = 0
line_count_status   = exact_rename
~~~

For every other event, required Git object contents are loaded directly. Missing
required objects fail closed. An absent add/delete side is empty bytes.

Gitlink/tree/non-blob/non-line events use:

~~~text
canonical_additions = 0
canonical_deletions = 0
line_count_status   = binary_or_unavailable
~~~

Blob events also use that status when either non-empty participating blob contains
NUL (`0x00`). V1 never substitutes byte counts, textconv, external diff, estimates,
or backend sentinels.

Otherwise line churn uses raw bytes, not decoded text:

1. split on LF `0x0A`;
2. LF is terminator, not payload;
3. CR and all other bytes remain payload;
4. empty bytes have zero lines;
5. terminal LF adds no extra trailing line;
6. equality is exact byte equality;
7. let `L` be the mathematical LCS length.

~~~text
canonical_deletions = old_line_count - L
canonical_additions = new_line_count - L
line_count_status   = text
~~~

Only LCS length matters, so Myers/histogram/patience choices, Git attributes,
textconv, and diff-script tie breaking cannot change totals.

`commit_count(f)` counts distinct canonical file-evidence commits, not raw delta
entries.

~~~text
churn(f) = sum(canonical_additions + canonical_deletions over canonical events)
~~~

Churn is volume, not complexity.

## Categories and normalization populations

Primary category derives from canonical path. Fixed order:

1. production
2. tests
3. docs
4. generated
5. build_ci
6. samples_examples
7. unknown

#237 analysis ignores happen before score populations and `G0`. Presentation
suppression is downstream and cannot rescore evidence.

File metrics normalize within primary-category cohorts. Base-edge metrics normalize
within unordered endpoint-category cohorts. Cross-cohort normalized scores are not
one globally comparable scale.

~~~text
normalized(x) = 0 if max(population)=0, else x/max(population)
normalized_log(x) = 0 if max(population)=0,
                    else log(1+x)/log(1+max(population))
~~~

Missing optional evidence is raw zero. Remaining weights are never renormalized.

## Canonical numeric model and weights

~~~text
Q(v) = round-half-to-even(v, 9 decimal places)
~~~

Components, proximity, edge weights, final scores, and thresholds are quantized
before threshold comparison, ranking, or serialization.

Default profiles:

| Profile | Weights |
| --- | --- |
| Hotspot | commit .30, churn .25, task .25, author .10, temporal .10 |
| Bottleneck | independent task .35, author .15, temporal .20, degree .20, centrality .10 |
| OCP | independent task .40, centrality .25, repeated edit .25, role hint .10 |
| Co-change | commit .75, task .25 |

Weights are finite non-negative ordinary base-10 decimals with at most nine
fractional digits. Positive means enabled, zero disabled. At least one component
is enabled. Every exact profile sums to `1.000000000`; co-change has
`alpha + beta = 1.000000000`. Validation happens before `Q`; bad sums are not
repaired. Evidence absence never changes enabledness or weights.

## Hotspots

~~~text
C_f = Q(normalized(commit_count(f)))
H_f = Q(normalized_log(churn(f)))
T_f = Q(normalized(distinct_canonical_task_keys(f)))
A_f = Q(normalized(distinct_authors(f)))
R_f = Q(normalized(temporal_span_seconds(f)))

HotspotScore(f) = Q(w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f)
~~~

Temporal span is exact latest-minus-earliest canonical committer epoch-second
integer. Rankings remain category-local; production is the primary human-facing
group.

## Base co-change graph `G0`

~~~text
G0 = (V,E0)
V  = retained logical files
E0 = { unordered(a,b) : CommitCoChange(a,b) > 0 }

CommitCoChange(a,b) = count(canonical file-evidence commits containing both)
TaskCoChange(a,b)   = count(distinct canonical TaskKeys whose episodes contain both)
~~~

Task evidence can weight an existing edge but cannot create topology when
`CommitCoChange=0`.

~~~text
CommitComponent  = Q(normalized(CommitCoChange))
TaskComponent    = Q(normalized(TaskCoChange))
CombinedCoChange = Q(alpha*CommitComponent + beta*TaskComponent)
~~~

Pair normalization/ranking is endpoint-category-cohort-local. Distinct-neighbor
and centrality evidence always uses `G0`.

## Threshold graph `Gtheta` and clusters

~~~text
Gtheta = (V, { e in E0 : CombinedCoChange(e) >= theta })
~~~

Threshold comparison is inclusive. `Gtheta` exists only for clusters and cluster-
derived candidates. Changing `theta` cannot alter `G0`, pair weights, `D_f`,
`K_f`, hotspot, bottleneck, or OCP scores.

~~~text
ClusterEdges(C) = qualifying Gtheta edges internal to C
ClusterMaximum(C) = max(CombinedCoChange(e) for e in ClusterEdges(C))
ClusterAggregate(C) = Q(sum(CombinedCoChange(e) for e in ClusterEdges(C)))
~~~

Sub-threshold internal `G0` edges do not enter the aggregate.

## Independent task evidence and temporal proximity

A multi-reference commit may contribute ordinary canonical TaskKey breadth and
TaskCoChange but does not prove independent work.

TaskKeys `x,y` are independent for file `f` only when each has at least one pair-
exclusive canonical file-evidence commit touching `f`. Shared-key commits do not
enter pair-exclusive intervals.

Pair-side intervals are closed intervals over exact arbitrary-precision committer
epoch-second integers:

~~~text
gap_seconds = later.start_epoch_second - earlier.end_epoch_second

days_between = 0                         if gap_seconds <= 0
               ceil(gap_seconds / 86400) if gap_seconds > 0

TemporalProximity(x,y) = Q(1/(1+days_between))
~~~

Calendar dates, timezone, local midnight, DST, bounded host date ranges, and
floating-point seconds never participate. A 25-hour gap gives `days_between=2`.

## Cohort-safe centrality and bottleneck score

~~~text
IncidentCommitDegree(f) = Σ CommitCoChange(f,n)
IncidentTaskDegree(f)   = Σ TaskCoChange(f,n)
IC_f = Q(normalized(IncidentCommitDegree(f))) within f's category
IT_f = Q(normalized(IncidentTaskDegree(f))) within f's category
K_f  = Q(alpha*IC_f + beta*IT_f)
~~~

V1 reuses co-change `alpha/beta` for centrality.

~~~text
T_f = Q(normalized(IndependentTaskSpread(f)))
A_f = Q(normalized(author_spread(f)))
O_f = Q(normalized(independent_temporal_proximity(f)))
D_f = Q(normalized(distinct_neighbor_degree_G0(f)))

BottleneckScore(f) = Q(b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f)
~~~

Rankings are category-local and describe pressure, not proven merge conflicts.

## OCP pressure and repeated edits

~~~text
Partners_f(t) = {u : canonical TaskKeys (t,u) independent for f}
PairExclusive_f(t,u) = {c touching f : c references t and not u}
Qualifying_f(t) = SHA-deduplicated union over Partners_f(t)
Repeated_f(t) = max(|Qualifying_f(t)| - 1, 0)
E_f = Σ Repeated_f(t)
~~~

A commit counts at most once per canonical TaskKey after the SHA union, regardless
of partner count.

## Portable role-token evidence

Starting from canonical filename stem:

1. characters outside `[A-Za-z0-9]` delimit tokens;
2. split lowercase -> uppercase;
3. split before final uppercase of an acronym when next character is lowercase;
4. split letter <-> digit;
5. map ASCII `A-Z` to `a-z`.

Non-ASCII characters are delimiters. Matching is exact equality only.

Default tokens:

~~~text
dispatcher, registry, handler, loader, session, options, configuration,
command, diagnostic, mapper, dto, model, service, orchestrator
~~~

`N_f = 1.000000000` when any token matches, otherwise zero.

~~~text
OcpPressureScore(f) = Q(o_t*T_f + o_c*K_f + o_r*Q(normalized(E_f)) + o_n*N_f)
~~~

## Ranking and candidates

Within one file category: descending score, ordinary canonical TaskKey spread,
churn, commit count, then scalar-value canonical path order.

`G0` pairs and `Gtheta` clusters rank only inside endpoint-category cohorts.
Candidates are evidence-derived investigations carrying source findings,
components, thresholds, cohort identity, and caveats. They are not automatic
redesign decisions.

## Successful reports vs fail-closed diagnostics

Canonical Markdown/JSON reports exist **only after successful analysis**. Invalid
refs, malformed/unreadable author or committer metadata, invalid selected author/
message/path UTF-8, TaskKey overlap ambiguity, missing required objects, or invalid
config produce a command/error diagnostic and no partial report, ranking, or
candidate set.

Diagnostics are a separate error surface. They identify a stable failure kind and
relevant object/span when available, but are not records inside a successful
canonical report and are not hashed as successful report bytes.

## Canonical JSON string and byte profile

Canonical JSON strings contain valid Unicode scalars and are never Unicode-
normalized during serialization.

Escaping is fixed:

- quote -> `\"`;
- backslash -> `\\`;
- backspace/tab/newline/formfeed/carriage-return -> short JSON escapes;
- other U+0000..U+001F -> `\u00XX` with uppercase hex;
- `/` remains literal;
- all other scalars, including non-ASCII, are direct UTF-8.

Canonical JSON bytes use UTF-8 without BOM, LF, two-space indentation, no trailing
whitespace, exactly one terminal LF, nine fractional digits for canonical reals,
no exponent notation, versioned #243 property order, and scalar-value order for
dynamic keys.

Report identity is over these exact bytes.

## Report semantics and limitations

Successful reports expose input/config/history identity, excluded merge count,
canonical committer epoch-second integers and timezone-token evidence, raw
metadata/task extraction provenance for successfully parsed evidence, canonical
TaskKeys/source evidence, accepted/`ambiguous_dag` rename evidence, canonical file
events/line-count status, categories/cohorts, raw/canonical components, effective
weights/thresholds, `G0`/clusters, bottleneck/OCP evidence, optional enrichment
status, candidates, and interpretation notes.

Optional .NET/Roslyn enrichment is downstream and cannot drop, change, rescore, or
reorder Git-level findings.

Reports state at least:

- churn is not complexity;
- co-change is not module proof;
- task/author evidence may be incomplete;
- non-UTF8 selected author/message metadata fails closed in v1;
- committer epoch seconds are exact integers; timezone token does not shift them;
- source task spellings normalize to canonical TaskKeys;
- multi-reference commits do not prove independent work;
- excluded merge deltas can understate merge-resolution edits;
- exact rename detection misses rename-with-edit;
- DAG-ambiguous or lifecycle-broken rename candidates do not collapse identity;
- accepted exact renames contribute zero content churn;
- NUL/gitlink/non-line events contribute zero line churn with explicit status;
- v1 requires strict UTF-8 Git paths and performs no Unicode normalization;
- normalized scores compare only inside their declared cohort;
- role hints are bounded heuristics;
- humans decide whether to refactor.

## Verification vectors for downstream implementation

At minimum cover:

- exact raw author/committer grammar and malformed/duplicate-header failure;
- timezone token not shifting epoch value and epoch integers outside host date range;
- raw author/message bytes, non-UTF8 fail-closed, ASCII-only author casing;
- `#001`/`#1`, `#0`, namespace separation, overlapping extractor collision;
- reachability independent of traversal order;
- root empty-tree delta and merge metadata-only behavior;
- strict UTF-8 path rejection;
- NFC/NFD distinction and non-BMP scalar ordering;
- formal candidate-overlap graph, linear exact rename, same-commit split, alias cycle,
  parallel DAG fork, path delete/recreate lifecycle break;
- pure accepted exact rename -> one touch/zero churn;
- DAG-ambiguous candidate -> ordinary add/delete events;
- raw-LF/LCS counts with ambiguous diff scripts;
- NUL/non-blob zero counts and missing-object failure;
- distinct-commit file `commit_count`;
- category isolation and numeric/weight goldens;
- task-only association not creating `G0`;
- threshold changes not affecting `D_f/K_f`/file scores;
- qualifying-edge-only cluster aggregate;
- multi-reference false-positive controls and SHA-union `E_f`;
- 25-hour gap -> `days_between=2`;
- mixed endpoint-category centrality;
- ASCII role-token vectors;
- fail-closed analysis emits no successful canonical report;
- canonical JSON escaping, scalar key order, and byte identity;
- empty range/all-zero evidence.

## Ownership

- #236: raw commit metadata, exact author/committer parsing and temporal integers,
  TaskKey extraction mechanics, reachability, Git objects/deltas, strict paths,
  candidate-overlap graph/DAG-safe exact renames/lifecycle breaks, canonical file
  events/LCS churn, CLI/error diagnostics.
- #237: schema-backed task extractor namespaces/patterns, classification, ignores,
  thresholds, effective profiles.
- #238: canonical hotspot scoring.
- #239: `G0`, `Gtheta`, pairs, clusters.
- #240: independent-TaskKey bottlenecks and exact temporal gaps.
- #241: repeated-edit/OCP evidence and role tokens.
- #242: optional downstream .NET enrichment.
- #243: versioned Markdown/canonical JSON successful-report schema/bytes and report
  vs diagnostic boundary.
- #244: dogfood and conformance/governance guardrails.
