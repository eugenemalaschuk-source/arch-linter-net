# Release Architecture Forensics Theory

This contributor reference is the readable theory authority for Release
Architecture Forensics introduced by #234. It is owned by #235 and synchronized
with the
[OpenSpec capability](../../openspec/specs/release-architecture-forensics/spec.md).

It describes planned behavior for #236–#244, not a currently shipped command or
policy surface.

## Product boundary

Current-state governance asks whether architecture is valid now. Release
forensics asks what architecture pressure accumulated across an explicit Git
range, which files became coordination bottlenecks, and what refactoring
investigations are justified by the evidence.

Canonical output is deterministic evidence. It is not formal proof of a design
law violation, merge conflict, or correct refactoring, and it does not require an
LLM.

## Canonical analysis identity and commit set

One analysis is identified by:

~~~text
(repository objects, explicit from ref, explicit to ref,
 effective history_analysis policy, history-semantics profile, tool version)
~~~

`from` is exclusive and `to` inclusive. Both refs must resolve or analysis fails
closed. The analyzed commit set is:

~~~text
Commits(from,to) = Reachable(to) \ Reachable(from)
~~~

`Reachable(r)` includes `r` and every commit reachable through parent edges.
Commits sort by committer UTC epoch second and then full SHA.

For a one-parent commit, file evidence is the parent-tree → commit-tree delta. A
root commit compares with the empty Git tree. Merge commits remain visible in
range metadata but contribute no v1 file-derived evidence. This avoids
first-parent/combined-diff ambiguity and double counting, while knowingly
understating merge-resolution-only edits.

## Canonical Git paths and ordering

Git paths are bytes. V1 accepts only strict UTF-8. Invalid UTF-8 fails closed
before classification, rename chaining, ranking, or JSON serialization. There is
no locale/code-page fallback and no replacement-character decoding.

Strict decoding preserves the exact Unicode scalar sequence. No Unicode
normalization (NFC/NFD/NFKC/NFKD) is applied. Consequently two Git paths that are
canonically equivalent Unicode text but use different scalar sequences remain
distinct.

Whenever the contract says `ordinal` ordering, use lexicographic Unicode scalar
numeric value:

1. compare corresponding scalars by numeric value;
2. the lower first differing scalar sorts first;
3. if one sequence is an exact prefix, the shorter sequence sorts first.

This rule is independent of UTF-16 code-unit ordering, UTF-32 representation,
locale collation, and filesystem collation. Canonical repository paths use `/`.

## Logical files and exact rename recognition

V1 intentionally does not use ambient Git rename heuristics. A canonical rename
is recognized only inside one non-merge commit when:

- exactly one path is deleted;
- exactly one path is added for that relation;
- deleted preimage and added postimage have the same Git blob object ID;
- no competing source or destination makes the relation ambiguous.

Similarity-based rename inference, copy inference, and rename-with-edit do not
participate in canonical identity. Split/copy/many-to-one/otherwise ambiguous
relationships stay separate.

A logical file is a linear chain of exact renames. Its canonical path is the last
in-range path, including a deleted path when deletion is final. Distinct earlier
non-canonical paths are aliases, ordered by first canonical occurrence then
canonical scalar-value path order. The canonical path is not duplicated among
aliases.

Examples:

~~~text
A -> B -> C    => canonical C, aliases [A, B]
A -> B -> A    => canonical A, aliases [B]
A -> {B, C}    => no rename chain; identities stay separate
~~~

## Canonical file events and line churn

There is one canonical file event per logical file per canonical file-evidence
commit.

A pure exact rename collapses its raw delete/add pair into one `rename` event.
It is one file touch but zero content churn:

~~~text
canonical_additions = 0
canonical_deletions = 0
canonical_churn     = 0
line_count_status   = exact_rename
~~~

For every other event, required Git object contents are loaded from the object
database. Missing required objects fail closed. An absent add/delete side is the
empty byte sequence.

Gitlink/tree/non-blob events, or any structurally non-line event, use zero line
counts and:

~~~text
line_count_status = binary_or_unavailable
~~~

For blob events, if either non-empty participating blob contains byte `0x00`, the
event is also `binary_or_unavailable` with zero additions/deletions. V1 never
substitutes byte counts, textconv, external-diff output, estimated lines, or
backend sentinels.

Otherwise line churn is computed from raw bytes, not decoded text:

1. split each blob on LF byte `0x0A`;
2. LF terminates a line and is not payload;
3. CR (`0x0D`) and every other byte remain payload;
4. empty bytes have zero lines;
5. a terminal LF does not create an extra trailing line;
6. line equality is exact byte-sequence equality;
7. let `L` be the mathematical longest-common-subsequence length of old/new line
   sequences.

Then:

~~~text
canonical_deletions = old_line_count - L
canonical_additions = new_line_count - L
line_count_status   = text
~~~

Only LCS length matters, so totals do not depend on diff-script tie-breaking,
Myers/histogram/patience choices, Git attributes, textconv, or backend heuristics.

`commit_count(f)` counts distinct canonical file-evidence commits touching the
logical file, not raw delta entries.

~~~text
churn(f) = sum(canonical_additions + canonical_deletions over canonical events)
~~~

Churn is change volume, not complexity. Exact renames and
binary/unavailable events intentionally contribute zero line churn and retain an
explicit status so the limitation is visible.

## Categories and normalization populations

Primary category derives from canonical path. Group order is:

1. production
2. tests
3. docs
4. generated
5. build_ci
6. samples_examples
7. unknown

#237 analysis ignores apply before score populations and `G0` construction.
Presentation suppression is downstream and cannot rescore evidence.

File metrics normalize within primary-category cohorts. Base-edge metrics
normalize within unordered endpoint-category cohorts. Cross-cohort normalized
scores are not one globally comparable scale.

~~~text
normalized(x) = 0 if max(population)=0, else x/max(population)
normalized_log(x) = 0 if max(population)=0,
                    else log(1+x)/log(1+max(population))
~~~

Missing optional evidence is raw zero. Remaining weights are never silently
renormalized.

## Canonical numeric model and weights

All canonical derived real values use:

~~~text
Q(v) = round-half-to-even(v, 9 decimal places)
~~~

Components, proximity, edge weights, final scores, and thresholds are reduced to
`Q(v)` before threshold comparison, ranking, or serialization.

Useful vectors:

~~~text
Q(1.2345678905) = 1.234567890
Q(1.2345678915) = 1.234567892
Q(log(51)/log(101)) = 0.851944303
~~~

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
T_f = Q(normalized(distinct_task_refs(f)))
A_f = Q(normalized(distinct_authors(f)))
R_f = Q(normalized(temporal_span_seconds(f)))

HotspotScore(f) = Q(w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f)
~~~

`temporal_span_seconds` is latest minus earliest canonical file-evidence
committer epoch second. Hotspots rank within their category. Production is the
primary human-facing group; cross-category normalized scores are not interleaved.

## Base co-change graph `G0`

~~~text
G0 = (V,E0)
V  = retained logical files
E0 = { unordered(a,b) : CommitCoChange(a,b) > 0 }

CommitCoChange(a,b) = count(canonical file-evidence commits containing both)
TaskCoChange(a,b)   = count(distinct tasks whose file episodes contain both)
~~~

Task co-change can weight an existing edge but cannot create topology when
`CommitCoChange=0`.

~~~text
CommitComponent  = Q(normalized(CommitCoChange))
TaskComponent    = Q(normalized(TaskCoChange))
CombinedCoChange = Q(alpha*CommitComponent + beta*TaskComponent)
~~~

Pair normalization and ranking are endpoint-category-cohort-local. Distinct
neighbor degree and centrality always use `G0`.

## Threshold graph `Gtheta` and clusters

~~~text
Gtheta = (V, { e in E0 : CombinedCoChange(e) >= theta })
~~~

Threshold comparison is inclusive. `Gtheta` exists only for clusters and
cluster-derived candidates. Changing `theta` cannot alter `G0`, pair weights,
`D_f`, `K_f`, hotspot, bottleneck, or OCP scores.

For cluster `C`:

~~~text
ClusterEdges(C) = qualifying Gtheta edges internal to C
ClusterMaximum(C) = max(CombinedCoChange(e) for e in ClusterEdges(C))
ClusterAggregate(C) = Q(sum(CombinedCoChange(e) for e in ClusterEdges(C)))
~~~

Sub-threshold internal `G0` edges do not enter the aggregate.

## Independent task evidence and temporal proximity

A multi-reference commit can contribute ordinary task breadth/co-change but does
not prove independent work. Refs `x,y` are independent for file `f` only when
each has at least one pair-exclusive canonical file-evidence commit touching
`f`.

Each side forms a closed interval over pair-exclusive committer epoch seconds.
Shared-reference commits do not enter those intervals.

~~~text
gap_seconds = later.start_epoch_second - earlier.end_epoch_second

days_between = 0                         if gap_seconds <= 0
               ceil(gap_seconds / 86400) if gap_seconds > 0

TemporalProximity(x,y) = Q(1/(1+days_between))
~~~

For positive integer gaps, `(gap_seconds + 86399) div 86400` is equivalent.
Calendar dates, timezone, local midnight, and DST never participate. A 25-hour
gap is therefore two `days_between`.

## Cohort-safe centrality and bottleneck score

Using `G0` neighbors:

~~~text
IncidentCommitDegree(f) = Σ CommitCoChange(f,n)
IncidentTaskDegree(f)   = Σ TaskCoChange(f,n)
IC_f = Q(normalized(IncidentCommitDegree(f))) within f's category
IT_f = Q(normalized(IncidentTaskDegree(f))) within f's category
K_f  = Q(alpha*IC_f + beta*IT_f)
~~~

V1 deliberately reuses co-change `alpha/beta` for centrality.

~~~text
T_f = Q(normalized(IndependentTaskSpread(f)))
A_f = Q(normalized(author_spread(f)))
O_f = Q(normalized(independent_temporal_proximity(f)))
D_f = Q(normalized(distinct_neighbor_degree_G0(f)))

BottleneckScore(f) = Q(b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f)
~~~

Rankings remain category-local. Findings are pressure signals, not claims that an
actual merge conflict occurred.

## OCP pressure and repeated edits

OCP uses the same independent-task spread and `G0`-derived `K_f`.

~~~text
Partners_f(t) = {u : (t,u) independent for f}
PairExclusive_f(t,u) = {c touching f : c references t and not u}
Qualifying_f(t) = SHA-deduplicated union over Partners_f(t)
Repeated_f(t) = max(|Qualifying_f(t)| - 1, 0)
E_f = Σ Repeated_f(t)
~~~

A commit counts at most once per task after the SHA union, regardless of how many
partners made it qualify.

## Portable role-token evidence

Starting from canonical filename stem:

1. characters outside `[A-Za-z0-9]` delimit tokens;
2. split lowercase → uppercase;
3. split before final uppercase of an acronym when next character is lowercase;
4. split letter ↔ digit;
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

Within one file category: descending score, ordinary task spread, churn, commit
count, then canonical scalar-value path order.

`G0` pairs and `Gtheta` clusters rank only inside their endpoint-category cohort.
Candidates are evidence-derived investigations carrying source findings,
components, thresholds, cohort identity, and caveats. They are not automatic
redesign decisions.

## Canonical JSON string and byte profile

Canonical JSON strings contain valid Unicode scalar sequences; no Unicode
normalization is added during serialization.

Escaping is fixed:

- quote → `\"`;
- backslash → `\\`;
- backspace/tab/newline/formfeed/carriage-return → short JSON escapes;
- other U+0000..U+001F → `\u00XX` with uppercase hex;
- `/` remains literal;
- all other scalars, including non-ASCII, are emitted directly as UTF-8, not
  optional `\uXXXX`/surrogate escapes.

Canonical JSON bytes use:

- UTF-8 without BOM;
- LF line endings;
- two-space indentation;
- no trailing whitespace;
- exactly one terminal LF;
- exactly nine fractional digits for canonical reals;
- no exponent notation for canonical reals;
- versioned #243 property order;
- canonical scalar-value order for dynamic map keys.

Report identity is over these exact bytes.

## Report semantics and limitations

Reports expose input/config/history identity, excluded merge count, canonical
file-event and line-count status, categories/cohorts, raw and canonical
components, effective weights/thresholds, `G0`/clusters, bottleneck/OCP evidence,
optional enrichment status, candidates, and interpretation notes.

Optional .NET/Roslyn enrichment is downstream and cannot drop, change, rescore,
or reorder Git-level findings.

Reports must state at least:

- churn is not complexity;
- co-change is not module proof;
- task/author evidence may be incomplete;
- multi-reference commits do not prove independent work;
- excluded merge deltas can understate merge-resolution edits;
- exact rename recognition misses rename-with-edit;
- exact renames contribute zero content churn;
- NUL/gitlink/non-line events contribute zero line churn with explicit status;
- v1 requires strict UTF-8 Git paths and applies no Unicode normalization;
- normalized scores compare only inside their declared cohort;
- role hints are bounded heuristics;
- humans decide whether to refactor.

## Verification vectors for downstream implementation

At minimum cover:

- reachability independent of traversal order;
- root empty-tree delta and merge metadata-only behavior;
- strict UTF-8 rejection;
- precomposed/decomposed and non-BMP scalar-order fixtures;
- exact rename cross-category/split/alias-cycle behavior;
- pure exact rename => one touch and zero churn;
- raw-LF/LCS line-count fixture with multiple equally valid diff scripts;
- NUL/non-blob => zero line counts plus marker;
- missing required blob => fail closed;
- distinct-commit file `commit_count`;
- category isolation and numeric half-even/log vectors;
- valid/invalid exact weight profiles;
- task-only association not creating `G0`;
- threshold changes not affecting `D_f/K_f`/file scores;
- qualifying-edge-only cluster aggregate;
- multi-reference false-positive controls and SHA-union `E_f`;
- 25-hour gap => `days_between=2`;
- mixed endpoint-category centrality;
- ASCII role-token vectors;
- canonical JSON escaping, scalar key order, and byte identity;
- empty range/all-zero evidence.

## Ownership

- #236: reachability, Git objects/deltas, strict path model, exact renames,
  canonical file events/LCS line churn, authors/tasks, CLI family.
- #237: schema-backed classification/ignores/thresholds/effective profiles.
- #238: canonical hotspot scoring.
- #239: `G0`, `Gtheta`, pairs, clusters.
- #240: independent-task bottlenecks and exact temporal gaps.
- #241: repeated-edit/OCP evidence and role tokens.
- #242: optional downstream .NET enrichment.
- #243: versioned Markdown/canonical JSON report schema and bytes.
- #244: dogfood and conformance/governance guardrails.
