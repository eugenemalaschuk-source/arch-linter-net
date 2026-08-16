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

## Canonical analysis identity and object IDs

One successful run is identified by:

~~~text
(repository objects, repository object-hash format,
 authored from operand, authored to operand,
 resolved canonical from commit ID, resolved canonical to commit ID,
 effective history_analysis policy, history-semantics profile, tool version)
~~~

Canonical Git object IDs use the repository-declared hash format and render the
full digest as lowercase ASCII hexadecimal, two characters per digest byte. SHA-1
therefore uses 40 hex characters and SHA-256 uses 64. Abbreviated object IDs are
not canonical v1 operands or evidence IDs.

Local checkout roots, locale, timezone, generated timestamps, process state, and
working-tree changes do not enter Git-only canonical identity.

## Deterministic authored-ref resolution

V1 accepts exactly four authored operand forms:

1. literal `HEAD`;
2. a full hexadecimal object ID matching the repository hash length;
3. a fully-qualified ref beginning `refs/`;
4. otherwise a shorthand looked up only as `refs/tags/<operand>` and
   `refs/heads/<operand>`.

A full-length hex operand is an object ID, not shorthand. `HEAD` resolves the
repository HEAD. A fully-qualified ref uses exact lookup. Shorthand succeeds only
when exactly one of tag/head exists; a branch/tag collision is an error. Remote
shorthand is not inferred; use a fully-qualified `refs/remotes/...` name.

Symbolic refs are recursively dereferenced with cycle detection. Annotated tag
objects are peeled recursively until a non-tag object is reached. The final object
must be a commit.

V1 does not evaluate revision-expression syntax, reflog selectors, path suffixes,
`~`, `^`, `^{...}`, or abbreviated object IDs. For example, `HEAD~2` is rejected
unless that exact text is itself a valid ref name under the rules above.

After resolution:

~~~text
Commits(from,to) = Reachable(to) \ Reachable(from)
~~~

`Reachable(r)` includes `r` and every parent-reachable commit, even when `from` is
not an ancestor of `to`.

## Raw commit metadata is canonical input

Commit author/message/time evidence is read from raw Git commit-object bytes, not
from `git log` or a library presentation API that may silently transcode or
calendar-convert metadata.

The raw commit object is parsed as LF-delimited headers followed by the first
empty header line and raw message payload. Required objects/headers that are
missing, malformed, or unreadable fail closed.

### Exact `author` header parsing

Exactly one direct `author ` header is required. After its literal prefix, parse
from right to left:

~~~text
<identity-bytes> SP <timestamp-token> SP <timezone-token>
~~~

- timestamp matches `-?[0-9]+`;
- timezone matches `[+-][0-9]{4}`;
- remaining identity bytes end in `>`;
- the final `>` and last `<` before it delimit email bytes;
- bytes before that `<` are name bytes.

Non-unique or malformed structure fails instead of delegating to backend identity
formatting.

Canonical author identity:

1. trim ASCII SP/HT from email bytes;
2. select non-empty email, otherwise ASCII-SP/HT-trimmed name;
3. strict-UTF8 decode selected bytes or fail closed;
4. trim only ASCII SP/HT;
5. lowercase ASCII `A-Z` only;
6. perform no Unicode normalization, locale casing, or full Unicode case folding;
7. use `unknown` only when both parsed email and name are empty.

### Exact `committer` epoch-second parsing

Exactly one direct `committer ` header is required and uses the same right-to-left
suffix grammar. Its timestamp token is an arbitrary-precision signed base-10 Unix
epoch-second integer. Leading zeroes and `-0` are spelling only; canonical numeric
zero is `0`.

The timezone token is validated and retained as metadata, but never added to or
subtracted from the epoch value. Canonical commit order compares exact epoch
integer first and full canonical commit ID second.

Every temporal span/gap uses exact integer arithmetic. Local dates, DST, timezone
conversion, floating seconds, or host `DateTime` ranges never participate.

### `encoding` headers are provenance, not transcoding instructions

Every direct `encoding ` header is canonical provenance even though it has no
semantic decoding effect. The raw value bytes after the prefix are rendered as
lowercase hexadecimal, two digits per byte, in original header order. No headers
means an empty array.

This makes canonical report content deterministic while preserving the rule that
legacy metadata is never silently transcoded.

## Canonical TaskKey identity and provenance

Task extraction runs on the **raw message payload** after strict UTF-8 decoding.
Invalid UTF-8 fails closed even when a Git client could transcode the payload using
an `encoding` header.

Each #237 extractor has a unique stable extractor ID
`[a-z][a-z0-9._-]*` and maps a match to:

- stable ASCII-lowercase namespace `[a-z][a-z0-9._-]*`;
- positive ASCII-decimal identifier `[0-9]+`.

Canonical identity is structural:

~~~text
TaskKey = (namespace, positive_decimal_id)
~~~

The decimal is arbitrary precision, greater than zero, and renders without leading
zeroes.

Every extractor match retains mandatory canonical provenance:

~~~text
(extractor_id, TaskKey, raw_message_byte_span[start,end), matched_utf8_text)
~~~

Spans are non-empty and half-open. Identical provenance records deduplicate.
Records order by start, end, extractor ID scalar order, then TaskKey order. The
TaskKey set deduplicates independently.

Overlapping spans that map to different TaskKeys fail closed rather than depending
on extractor ordering or nesting. Non-overlapping references may remain distinct.

### Default issue extractor

The default extractor has extractor ID `issue`, namespace `issue`, and matches a
literal `#` plus ASCII digits with numeric value greater than zero. The scalar
immediately before `#`, when present, and immediately after the final digit, when
present, must both be outside `[A-Za-z0-9_#]`.

The matched span contains exactly `#` plus digits.

~~~text
#001     -> (issue, 1)
#1       -> (issue, 1)
#0       -> no TaskKey
(#12)    -> (issue, 12)
abc#12   -> no match
#12foo   -> no match
##12     -> no match
#12#13   -> no match
~~~

All task spread, task episodes, TaskCoChange, independent pairs, and repeated-edit
evidence consume canonical TaskKeys rather than source spellings.

## Canonical commit set and merge policy

After ref resolution:

~~~text
Commits(from,to) = Reachable(to) \ Reachable(from)
~~~

Commits sort by exact committer epoch integer then full commit ID.

A one-parent commit uses the parent-tree -> commit-tree delta. A root commit uses
the empty tree. Merge commits remain in range metadata but contribute no v1 file-
derived evidence. This avoids first-parent/combined-diff ambiguity and double
counting while knowingly understating merge-resolution-only edits.

## Canonical Git paths and ordering

Git paths are bytes. V1 accepts only strict UTF-8. Invalid UTF-8 fails closed
before classification, identity, ranking, or JSON. There is no locale/code-page
fallback or replacement decoding.

Strict decoding preserves exact Unicode scalar sequence. NFC/NFD/NFKC/NFKD
normalization is forbidden, so canonically equivalent but scalar-distinct path
spellings remain distinct.

Canonical ordinal ordering means lexicographic Unicode scalar numeric value:

1. compare corresponding scalars numerically;
2. lower first differing scalar sorts first;
3. exact-prefix shorter sequence sorts first.

Host UTF-16 ordering, filesystem collation, locale collation, and normalization
libraries are not authoritative. Repository paths use `/`.

## Baseline same-path identity

Before accepted rename unions, all canonical non-merge file events whose canonical
repository path strings are exactly equal belong to **one baseline path identity
for the whole analyzed commit set**.

V1 does not split that identity when the path is deleted/re-added, gets unrelated
blob content, or appears on different reachable branches. This is a deliberate
simplification: path reuse can over-aggregate unrelated file generations. Reports
must surface that limitation. Lifetime segmentation would be a future semantic-
profile change.

Thus:

~~~text
modify X ; delete X ; later add unrelated X
=> one baseline X identity in v1
~~~

Accepted exact-rename lineages may union several baseline path identities.
`ambiguous_dag` components perform no cross-path union.

## Exact rename candidates and Git-DAG/lifecycle safety

Inside one non-merge commit, a local exact-rename candidate exists only when a
one-to-one delete/add relation has identical preimage/postimage blob ID and no
competing same-commit source or destination.

Similarity, copy inference, rename-with-edit, candidate thresholds, and Git client
configuration cannot create canonical candidates.

For candidate `c`:

~~~text
Endpoints(c) = { src(c), dst(c) }
~~~

Build undirected overlap graph `H` whose candidate vertices connect exactly when
endpoint sets intersect. Connected components of `H` are potential rename
lineages.

A component canonicalizes only if exactly one permutation `(c1,...,ck)` contains
all candidates and:

1. every earlier candidate commit is a strict ancestor of every later one;
2. each `dst(ci) == src(c{i+1})`;
3. for a shared adjacent path `p`, no ordinary canonical add/delete of `p` occurs
   in a non-merge commit strictly between the two candidate commits.

Rule 3 is the lifecycle guard: deleting and later recreating the same path breaks
rename-chain continuity even though all same-path events still belong to one v1
baseline path identity.

A fork/join, non-unique sequence, or lifecycle break is `ambiguous_dag`. No
candidate in that component collapses identity. Timestamp/object-ID order never
repairs topology or pathname reuse ambiguity.

Examples:

~~~text
A -> B -> C on descendants, no lifecycle break => one logical file, canonical C
A -> B -> A on descendants                    => canonical A, alias B
branch 1: A -> B
branch 2: A -> C                              => ambiguous_dag
A -> B ; delete B ; add B ; B -> C            => ambiguous_dag lifecycle break
~~~

For an accepted lineage, baseline path identities in the sequence are unioned.
The terminal destination is canonical path unless a later event on that same
terminal path deletes it. Distinct historical non-canonical path strings are
aliases ordered by first canonical occurrence then scalar-value path order.

### Rename provenance is mandatory

Every local candidate is canonical provenance, including ambiguous ones. A
candidate record contains canonical commit ID, source path, destination path,
canonical blob object ID, component membership/status, and accepted/ambiguous
outcome.

Candidate records order by canonical commit order, source path, destination path,
then blob object ID. Candidate lists within a component use the same order;
components order by their minimum candidate record key.

This evidence is part of successful canonical JSON, not optional debug data.

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

A candidate in `ambiguous_dag` does not collapse; its delete/add entries remain
ordinary events.

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

Otherwise line churn uses raw bytes:

1. split on LF `0x0A`;
2. LF is terminator, not payload;
3. CR and all other bytes remain payload;
4. empty bytes have zero lines;
5. terminal LF adds no extra trailing line;
6. equality is exact byte equality;
7. let `L` be mathematical LCS length.

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
globally comparable.

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
or ambiguous refs, malformed/unreadable metadata, invalid selected author/message/
path UTF-8, TaskKey overlap ambiguity, missing required objects, or invalid config
produce a command/error diagnostic and no partial report, ranking, or candidate
set.

Diagnostics are a separate error surface. They identify a stable failure kind and
relevant object/span when available, but are not records inside a successful
canonical report and are not hashed as successful report bytes.

## Mandatory canonical provenance

Successful canonical JSON always contains the canonical provenance that affects
interpretability/reproducibility:

- repository object-hash format, authored operands, resolved full commit IDs;
- exact committer epoch integers plus raw timezone tokens;
- every `encoding ` header raw value as lowercase-hex bytes in original order;
- canonical authors;
- complete ordered TaskKey match-provenance records plus deduplicated TaskKeys;
- complete ordered local rename-candidate/component records, including ambiguous
  candidates and accepted/ambiguous outcome.

This provenance is mandatory, not optional debug evidence. Extra debug-only data
may exist outside canonical JSON but cannot affect canonical report identity.

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

Canonical JSON bytes use:

- UTF-8 without BOM;
- LF;
- two-space indentation;
- no trailing whitespace;
- exactly one terminal LF;
- lowercase full Git object IDs;
- exact non-exponent integers for counts, TaskKey IDs, epoch seconds, and gaps;
- exactly nine fractional digits for canonical reals;
- no exponent notation for canonical reals;
- versioned #243 property order;
- scalar-value order for dynamic map keys and the explicit provenance orders above.

Report identity is over these exact bytes.

## Report semantics and limitations

Successful reports expose input/config/history identity, excluded merge count,
mandatory provenance, canonical file events/line-count status, categories/cohorts,
raw/canonical components, effective weights/thresholds, `G0`/clusters,
bottleneck/OCP evidence, optional enrichment status, candidates, and interpretation
notes.

Optional .NET/Roslyn enrichment is downstream and cannot drop, change, rescore, or
reorder Git-level findings.

Reports state at least:

- churn is not complexity;
- co-change is not module proof;
- task/author evidence may be incomplete;
- non-UTF8 selected author/message metadata fails closed in v1;
- committer epoch seconds are exact integers and timezone token does not shift them;
- source task spellings normalize to canonical TaskKeys;
- multi-reference commits do not prove independent work;
- excluded merge deltas can understate merge-resolution edits;
- exact rename detection misses rename-with-edit;
- DAG-ambiguous/lifecycle-broken rename candidates do not collapse identity;
- v1 aggregates delete/recreate events at the same pathname into one baseline
  identity and may conflate unrelated pathname generations;
- accepted exact renames contribute zero content churn;
- NUL/gitlink/non-line events contribute zero line churn with explicit status;
- v1 requires strict UTF-8 Git paths and performs no Unicode normalization;
- normalized scores compare only inside their declared cohort;
- role hints are bounded heuristics;
- humans decide whether to refactor.

## Verification vectors for downstream implementation

At minimum cover:

- full Git object-ID formats and deterministic ref resolution;
- branch/tag shorthand collision, annotated tag peeling, unsupported `HEAD~2`;
- exact raw author/committer grammar and malformed/duplicate-header failure;
- timezone token not shifting epoch and epoch integers outside host date range;
- raw author/message bytes, non-UTF8 fail-closed, ASCII-only author casing;
- `encoding ` header provenance arrays;
- `#001`/`#1`, `#0`, namespace separation, overlapping extractor collision,
  default lexical-boundary vectors;
- TaskKey provenance ordering/deduplication;
- reachability independent of traversal order;
- root empty-tree delta and merge metadata-only behavior;
- strict UTF-8 path rejection;
- NFC/NFD distinction and non-BMP scalar ordering;
- plain same-path delete/readd baseline identity;
- formal candidate-overlap graph, linear exact rename, same-commit split, alias cycle,
  parallel DAG fork, path delete/recreate lifecycle break;
- mandatory rename provenance ordering;
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
- canonical JSON escaping, scalar/provenance order, and byte identity;
- empty range/all-zero evidence.

## Ownership

- #236: object/ref resolution, raw commit metadata, exact author/committer parsing,
  temporal integers, TaskKey extraction/provenance mechanics, reachability,
  Git objects/deltas, strict paths, baseline same-path identity,
  candidate-overlap graph/DAG-lifecycle-safe exact renames, canonical file
  events/LCS churn, CLI/error diagnostics.
- #237: schema-backed task extractor IDs/namespaces/patterns, classification,
  ignores, thresholds, effective profiles.
- #238: canonical hotspot scoring.
- #239: `G0`, `Gtheta`, pairs, clusters.
- #240: independent-TaskKey bottlenecks and exact temporal gaps.
- #241: repeated-edit/OCP evidence and role tokens.
- #242: optional downstream .NET enrichment.
- #243: versioned successful Markdown/canonical JSON schema/bytes, mandatory
  provenance serialization, and report-vs-diagnostic boundary.
- #244: dogfood and conformance/governance guardrails.
