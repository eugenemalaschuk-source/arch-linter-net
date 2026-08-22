# release-architecture-forensics Specification

## Purpose

Define deterministic Git-range evidence, scoring, findings, recommendations, and
canonical report semantics for Release Architecture Forensics.
## Requirements
### Requirement: Explicit deterministic analysis identity
Release Architecture Forensics SHALL analyze an explicit Git range with
exclusive `from` and inclusive `to`. Both operands SHALL resolve to commits before
analysis; missing, ambiguous, unsupported, or unresolvable operands SHALL fail
closed.

Canonical identity SHALL contain authored operands, resolved canonical commit
object IDs, repository Git object-hash format, effective `history_analysis`
configuration identity, history-semantics profile identity, and tool version. It
SHALL exclude absolute checkout roots, generated timestamps, timezone, locale,
process state, and other environment-dependent presentation data. Uncommitted
working-tree state SHALL NOT alter Git-only evidence.

#### Scenario: Equivalent environments
- **WHEN** identical repository objects, authored operands, effective configuration, history-semantics profile, and tool version are analyzed in different environments
- **THEN** canonical identity, evidence, rankings, and canonical JSON bytes are identical

### Requirement: Canonical Git object IDs and authored-ref resolution
The repository-declared Git object-hash format SHALL be canonical analysis input.
Canonical object-ID text SHALL be the full raw digest rendered as lowercase ASCII
hexadecimal with exactly two characters per digest byte. For SHA-1 repositories
this is 40 hexadecimal characters; for SHA-256 repositories this is 64. Short or
abbreviated object IDs SHALL NOT be canonical operands in v1.

Each authored `from`/`to` operand SHALL resolve by exactly one of these mutually
exclusive v1 forms:

1. literal `HEAD`;
2. a full hexadecimal object ID whose length matches the repository object format;
3. a fully-qualified ref name beginning with literal `refs/`;
4. otherwise, an unqualified shorthand looked up against exactly
   `refs/tags/<operand>` and `refs/heads/<operand>`.

A full-length hexadecimal operand SHALL be interpreted as an object ID, not as a
shorthand ref name. `HEAD` SHALL resolve the repository's HEAD symbolic/detached
reference. Fully-qualified refs SHALL use exact ref-name lookup. For an
unqualified shorthand, exactly one of the tag/head candidates SHALL exist; zero
is unresolved and two is ambiguous. Remote-ref shorthand is not inferred; callers
may use a fully-qualified `refs/remotes/...` name when desired.

Symbolic refs SHALL be dereferenced recursively with cycle detection. The
resolved object SHALL then peel zero or more annotated tag objects by following
their target object IDs until a non-tag object is reached. The final object MUST
be a commit. Missing objects, symbolic/tag cycles, or a final tree/blob/other
object SHALL fail closed.

V1 authored operands SHALL NOT interpret Git revision-expression grammar,
reflog selectors, path suffixes, `~`/`^` ancestry expressions, `^{...}` operators,
or abbreviated object IDs. Such inputs may only succeed when they are themselves
valid exact ref names under the rules above.

Resolved commit IDs SHALL be retained canonically using lowercase full object-ID
text. All blob/tree/commit/tag IDs retained in canonical evidence SHALL use the
same lowercase full-digest representation.

#### Scenario: Annotated tag peeling
- **WHEN** shorthand `v1.2.3` resolves only to `refs/tags/v1.2.3` and that ref points through annotated tag objects to a commit
- **THEN** the operand resolves deterministically to the peeled commit object ID

#### Scenario: Branch/tag shorthand collision
- **WHEN** both `refs/tags/release` and `refs/heads/release` exist
- **THEN** authored shorthand `release` fails as ambiguous rather than using Git DWIM precedence

#### Scenario: Unsupported revision expression
- **WHEN** authored operand `HEAD~2` is not itself an exact ref name under the v1 lookup rules
- **THEN** resolution fails instead of evaluating revision-expression syntax

### Requirement: Canonical raw commit metadata and author identity
Commit metadata used by canonical evidence SHALL be parsed from raw Git commit
object bytes. Implementations SHALL NOT use a `git log`/library presentation API
that may transcode, normalize, replace, or locale-decode author/message content
before canonical parsing.

The raw commit object SHALL be structurally parsed using LF-delimited commit
headers and the first empty header line as the message separator. Required commit
objects and required headers SHALL be readable and structurally valid; otherwise
analysis SHALL fail closed. Header continuation lines belonging to unrelated
headers MAY be retained outside canonical report evidence but SHALL NOT alter
recognition of direct `author `, `committer `, or `encoding ` header lines.

Exactly one canonical `author` header SHALL be present. Its bytes after the
literal `author ` prefix SHALL be parsed from right to left as:

```text
<identity-bytes> SP <timestamp-token> SP <timezone-token>
```

The timestamp token SHALL match `-?[0-9]+`. The timezone token SHALL match
`[+-][0-9]{4}`. The remaining `identity-bytes` SHALL end in `>`. The final `>` and
the last `<` preceding it delimit the email bytes; bytes before that `<` are the
name bytes. Any structure that cannot be parsed uniquely by this rule SHALL fail
closed rather than delegating identity parsing to a Git/runtime formatter.

For canonical author identity:

1. trim leading/trailing ASCII SP (`0x20`) and HT (`0x09`) from the parsed email
   byte slice;
2. when the trimmed email is non-empty, select it; otherwise trim ASCII SP/HT from
   the parsed name byte slice and select the name;
3. the selected raw byte slice SHALL decode as strict UTF-8 or analysis fails;
4. trim only leading/trailing ASCII SP and HT from the decoded scalar sequence;
5. map ASCII `A-Z` to `a-z` and leave every other scalar unchanged;
6. apply no Unicode normalization, locale casing, or full Unicode case folding;
7. use literal `unknown` only when both parsed email and name are empty after the
   canonical ASCII trim.

Every direct `encoding ` header SHALL be retained as canonical provenance even
though it SHALL NOT affect decoding. Its raw value bytes after the literal prefix
SHALL be represented as lowercase hexadecimal, two digits per byte, in original
header order. Zero headers therefore yield an empty canonical array. This preserves
what the commit claimed without introducing legacy transcoding or an optional
canonical-report field.

#### Scenario: Legacy author bytes
- **WHEN** the selected author email/name bytes are not valid UTF-8
- **THEN** analysis fails closed instead of using locale/code-page decoding or replacement characters

#### Scenario: Portable author normalization
- **WHEN** two runtimes have different Unicode/culture casing behavior
- **THEN** canonical author identity is unchanged because only ASCII `A-Z` is lowercased

#### Scenario: Ambiguous author structure
- **WHEN** an `author` header cannot be uniquely parsed into identity, timestamp, timezone, final angle-bracketed email, and name by the canonical rule
- **THEN** analysis fails closed instead of using backend-specific identity parsing

### Requirement: Canonical committer timestamp and temporal integer domain
Exactly one canonical `committer` header SHALL be present. Its bytes after the
literal `committer ` prefix SHALL use the same right-to-left suffix grammar:

```text
<identity-bytes> SP <timestamp-token> SP <timezone-token>
```

The timestamp token SHALL match `-?[0-9]+` and SHALL be interpreted as an
arbitrary-precision signed base-10 integer number of Unix epoch seconds. Leading
zeroes and negative zero are representation details only; the canonical integer
value SHALL render without leading zeroes and SHALL render zero as `0`. The
`[+-][0-9]{4}` timezone token SHALL be structurally validated and retained as raw
metadata evidence but SHALL NOT be added to, subtracted from, or otherwise used
to transform the epoch-second value.

Canonical commit order SHALL compare this exact numeric committer epoch-second
value first and canonical lowercase full commit object ID second. Every temporal
span, interval boundary, gap, and difference in this capability SHALL use exact
integer arithmetic over these epoch-second values. Implementations SHALL NOT
route canonical temporal math through local dates, wall-clock conversion, bounded
`DateTime` ranges, or floating-point seconds. A malformed/missing/duplicate
canonical committer header SHALL fail analysis closed.

#### Scenario: Timezone token does not shift epoch
- **WHEN** two commits carry the same committer timestamp integer with different valid timezone tokens
- **THEN** their canonical epoch-second values are equal and canonical commit ID is the ordering tie-breaker

#### Scenario: Large epoch value
- **WHEN** a valid committer timestamp lies outside a host date/time library's supported calendar range
- **THEN** canonical analysis still uses the exact integer value rather than overflowing or rejecting it solely because of that library range

### Requirement: Canonical task-reference extraction, provenance, and identity
Task-reference extraction SHALL operate on the raw commit-message payload bytes
from the commit object, after the header/message separator. The raw payload SHALL
decode as strict UTF-8 before any configured extractor runs. Invalid UTF-8 SHALL
fail analysis closed; commit `encoding ` headers SHALL NOT trigger canonical
transcoding.

#237 owns bounded extractor configuration. Every effective extractor SHALL have a
unique stable extractor ID matching `[a-z][a-z0-9._-]*` and SHALL map a match to
exactly:

- one stable ASCII-lowercase namespace matching `[a-z][a-z0-9._-]*`;
- one positive ASCII-decimal identifier captured from `[0-9]+`.

The canonical task identity SHALL be the structural tuple:

```text
TaskKey = (namespace, positive_decimal_id)
```

The decimal identifier SHALL be interpreted at arbitrary precision and SHALL be
greater than zero. It SHALL render without leading zeroes. Consequently
`issue#001` and `issue#1` are one canonical key, while `issue#1` and `jira#1` are
distinct.

Every extractor match SHALL retain a mandatory canonical provenance record
containing extractor ID, canonical TaskKey, non-empty half-open raw message byte
span `[start,end)`, and the exactly matched decoded UTF-8 substring. Identical
provenance records SHALL deduplicate. Provenance records SHALL order by ascending
`start`, ascending `end`, canonical scalar-value extractor ID, then canonical
TaskKey order. The canonical TaskKey set SHALL deduplicate independently of the
provenance collection.

If two matches with overlapping raw byte spans map to different canonical
TaskKeys, extraction SHALL fail closed with an ambiguity diagnostic rather than
choosing extractor order or nested competing interpretations. Non-overlapping
matches MAY produce distinct TaskKeys. TaskKeys SHALL order first by canonical
scalar-value namespace order and then by exact numeric identifier value.

The default v1 issue extractor SHALL have extractor ID `issue`, namespace `issue`,
and match a literal `#` followed by one or more ASCII digits whose numeric value
is greater than zero. The scalar immediately before `#`, when present, and the
scalar immediately after the final digit, when present, SHALL each be outside
`[A-Za-z0-9_#]`. The matched span contains exactly `#` plus its digits. Thus
`(#12)` and `fix #12, #13.` match, while `abc#12`, `#12foo`, `##12`, `#12#13`,
and `#0` do not produce default TaskKeys.

All ordinary task spread, task episodes, TaskCoChange, independent-task pairs,
and repeated-edit evidence SHALL consume canonical TaskKeys rather than source
spellings.

#### Scenario: Leading-zero task identity
- **WHEN** one commit contains `#001` and another contains `#1` under the default extractor
- **THEN** both map to canonical TaskKey `(issue,1)` and do not manufacture two tasks

#### Scenario: Default lexical boundaries
- **WHEN** a message contains `abc#12 #12foo ##12 #12#13 (#14)`
- **THEN** the default extractor emits only canonical TaskKey `(issue,14)`

#### Scenario: Namespaced identities
- **WHEN** configured extractors produce `(issue,42)` and `(jira,42)` from non-overlapping matches
- **THEN** the two canonical TaskKeys remain distinct

#### Scenario: Overlapping extractor collision
- **WHEN** overlapping message byte spans are claimed by extractors and map to different TaskKeys
- **THEN** analysis fails closed instead of depending on extractor iteration or nesting order

#### Scenario: Non-UTF8 commit message
- **WHEN** raw commit-message bytes are not valid UTF-8
- **THEN** canonical task extraction fails closed even if a Git client could transcode them using an `encoding` header

### Requirement: Canonical commit set and file-touch deltas
After refs resolve, the canonical analyzed commit set SHALL be:

```text
Commits(from,to) = Reachable(to) \ Reachable(from)
```

`Reachable(r)` includes commit `r` itself and all commits reachable from it by
following zero or more parent edges. This rule applies whether or not `from` is
an ancestor of `to`.

Canonical commit order SHALL be ascending by the exact canonical committer epoch-
second integer, then ascending canonical lowercase full commit object ID.

For a non-merge commit with exactly one parent, canonical file-touch evidence
SHALL come from the tree delta from that parent to the commit. For a root commit,
the canonical parent tree SHALL be the empty Git tree.

For the initial deterministic profile, a merge commit with two or more parents
SHALL remain present in analyzed-range metadata but SHALL NOT contribute file
touches, churn, per-file commit count, per-file author spread, task-episode file
membership, rename evidence, co-change evidence, or any downstream file score.
Reports SHALL expose the excluded merge count and state that merge-resolution-only
edits may therefore be understated.

Only commits contributing canonical file-touch evidence participate in file-level
temporal spans. Merge timestamps remain range metadata only.

#### Scenario: Reachability range
- **WHEN** a side-branch commit is reachable from `to` but not from `from`
- **THEN** it belongs to the analyzed commit set regardless of traversal or first-parent enumeration

#### Scenario: Merge does not double-count
- **WHEN** two non-merge branch commits touch a file and a later merge joins those branches
- **THEN** the branch commits contribute file evidence while the merge remains metadata-only

#### Scenario: Root commit
- **WHEN** a root commit belongs to the analyzed range
- **THEN** its file delta is computed against the empty tree

#### Scenario: Empty range
- **WHEN** `Reachable(to) \ Reachable(from)` is empty
- **THEN** analysis succeeds with deterministic empty/zero evidence

### Requirement: Canonical Git path text and string ordering
This requirement SHALL preserve canonical Git path text and ordering semantics.
Git tree paths are byte sequences. Every path participating in canonical evidence
in the initial profile SHALL decode as strict UTF-8. Invalid, overlong, truncated,
or otherwise ill-formed UTF-8 SHALL fail analysis closed before classification,
rename chaining, ranking, or JSON serialization.

Implementations SHALL NOT use locale/code-page fallback, replacement characters,
or platform filesystem decoding. Strict UTF-8 decoding SHALL preserve the exact
Unicode scalar sequence encoded by the Git path bytes. Unicode normalization
(NFC, NFD, NFKC, NFKD) SHALL NOT be applied.

Canonical string ordering for paths, aliases, dynamic-map keys, and any other
field specified as ordinal SHALL be lexicographic by Unicode scalar numeric value.
At the first differing scalar, the lower scalar value sorts first; when one scalar
sequence is an exact prefix of the other, the shorter sequence sorts first.
This definition SHALL NOT depend on UTF-16 code-unit order, UTF-32 representation,
locale collation, filesystem collation, or Unicode normalization libraries.
Repository-relative `/` separators are canonical.

#### Scenario: Non-UTF8 Git path
- **WHEN** a path's raw Git bytes are not valid UTF-8
- **THEN** analysis fails instead of locale-decoding or inserting replacement characters

#### Scenario: Canonically distinct Unicode spellings
- **WHEN** two valid UTF-8 paths encode canonically equivalent but scalar-distinct Unicode sequences such as precomposed and decomposed accents
- **THEN** they remain distinct paths and sort by their actual decoded scalar sequences without Unicode normalization

#### Scenario: Supplementary scalar ordering
- **WHEN** canonical strings contain valid non-BMP Unicode scalars
- **THEN** ordering follows scalar numeric values rather than host-language code-unit ordering

### Requirement: Baseline same-path identity in v1
This requirement SHALL preserve one baseline identity for each exact pathname.
Before accepted rename lineages are applied, all canonical non-merge file events
whose canonical repository-relative path strings are exactly equal SHALL belong to
one baseline path identity for the entire analyzed commit set. V1 SHALL NOT split
that baseline identity merely because the path is deleted and later re-added,
changes blob identity, or appears on different reachable branches.

Accepted exact-rename lineages MAY union several baseline path identities into one
logical file according to the DAG-safe rules below. An `ambiguous_dag` component
SHALL perform no such cross-path union. Because one baseline identity exists per
exact path string, v1 logical files retain a unique canonical path after accepted
rename union and do not require a separate same-path lifetime discriminator.

This rule is deliberately conservative in implementation complexity and MAY
conflate unrelated generations that reuse the same pathname after deletion. That
known false-positive/over-aggregation risk SHALL be disclosed in report
limitations. Introducing path-lifetime segmentation is a future semantic-profile
change, not an implementation-local choice.

#### Scenario: Plain delete and recreate
- **WHEN** path `src/X.cs` is modified, deleted, later re-added with unrelated blob content, and no accepted rename lineage changes its path
- **THEN** v1 treats all those `src/X.cs` events as one baseline logical-file identity and reports pathname-reuse conflation as a limitation

### Requirement: Canonical exact-rename candidates and DAG-safe logical identity
Inside each canonical non-merge commit delta, a local exact-rename candidate SHALL
exist only for a one-to-one delete/add relation whose deleted preimage and added
postimage have exactly the same Git blob object identity and have no competing
source or destination in that commit.

Similarity-based rename inference, copy inference, rename-with-edit, ambient Git
rename thresholds, candidate limits, client configuration, and backend rename
heuristics SHALL NOT create local canonical candidates.

For every local candidate `c`, define `src(c)`, `dst(c)`, `commit(c)`, and the
canonical lowercase full blob object ID. Build an undirected candidate-overlap
graph `H` whose vertices are local candidates and whose edges connect two distinct
candidates exactly when their endpoint-path sets intersect:

```text
Endpoints(c) = { src(c), dst(c) }
H edge(c1,c2) iff Endpoints(c1) ∩ Endpoints(c2) != ∅
```

The connected components of `H` are the potential lineage components. This graph,
not input enumeration order, defines which candidates connect or compete.

A component with candidates `C` is canonicalizable only when there exists exactly
one permutation `(c1,...,ck)` containing every candidate in `C` such that:

1. for every `i < j`, `commit(ci)` is a strict Git ancestor of `commit(cj)`;
2. for every adjacent pair, `dst(ci) = src(c{i+1})`; and
3. for every adjacent pair sharing path `p = dst(ci)`, there is no ordinary
   canonical add or delete event for path `p` in a non-merge commit `d` strictly
   between the candidate commits (`commit(ci)` is an ancestor of `d`, and `d` is
   an ancestor of `commit(c{i+1})`). Such an intervening add/delete is a lifecycle
   break and prevents chaining across path deletion/recreation.

If no such all-candidate permutation exists, more than one exists, or a lifecycle
break exists, the component is `ambiguous_dag`. None of its candidates SHALL
collapse logical-file identity. Canonical timestamp/object-ID ordering SHALL NOT
turn ancestry-incomparable commits or reused path names into a rename chain.

For a canonicalizable component, the unique sequence SHALL union the baseline
path identities corresponding to every source/destination path in the sequence
into one logical file. Its canonical path SHALL be the terminal destination path
of the accepted candidate sequence unless a later canonical event on that same
terminal path deletes it, in which case that deleted path remains the canonical
path. Distinct historical non-canonical path strings SHALL be aliases, ordered by
first in-range occurrence using canonical commit order and then canonical scalar-
value string order. The canonical path SHALL NOT also appear in aliases.

For an `ambiguous_dag` component, involved baseline path identities remain
separate. Every local candidate SHALL nevertheless be retained as mandatory
canonical provenance with canonical commit ID, source path, destination path,
blob object ID, component membership/status, and accepted/ambiguous outcome. Its
raw delete/add entries SHALL continue as separate canonical file events and SHALL
NOT receive exact-rename zero-churn treatment.

Canonical local-candidate records SHALL order by canonical commit order, source
path, destination path, then blob object ID. A lineage-component record SHALL
order its candidate records by that same order; components SHALL order by their
minimum candidate record key. #243 SHALL serialize every such record; this is not
optional debug evidence.

#### Scenario: Exact rename across categories
- **WHEN** one commit exactly moves blob `src/Old.cs` to `tests/New.cs` with no competing source or destination and no DAG ambiguity
- **THEN** one logical file uses `tests/New.cs` as canonical path and retains `src/Old.cs` as an alias

#### Scenario: Modified move is not an exact candidate
- **WHEN** a delete/add pair has different blob object identities
- **THEN** the initial profile keeps both paths as separate logical identities

#### Scenario: Same-commit split is not a candidate
- **WHEN** one deleted blob has two same-blob added destinations in one commit
- **THEN** no local exact-rename candidate is created for that competing relation

#### Scenario: Linear alias cycle
- **WHEN** ancestry-ordered candidates move `A` to `B` and later `B` to `A` without a lifecycle break
- **THEN** the unique lineage canonicalizes to path `A` with alias `B`

#### Scenario: Parallel rename fork
- **WHEN** two ancestry-incomparable branch commits independently contain exact candidates `A -> B` and `A -> C`
- **THEN** their overlap component is `ambiguous_dag`, canonical commit timestamps do not select a winner, and `A`, `B`, and `C` remain separate baseline identities

#### Scenario: Path deletion and recreation breaks lineage
- **WHEN** an accepted-looking `A -> B` candidate is followed by an ordinary deletion and later ordinary recreation of `B` before a `B -> C` candidate
- **THEN** the overlap component is `ambiguous_dag`; `A` and `C` are not unioned with baseline path `B`, while all same-path `B` events still belong to the single v1 baseline `B` identity

### Requirement: Canonical file events, binary classification, and line churn
Canonical file evidence SHALL contain one logical-file event per logical file per
canonical file-evidence commit.

After a local exact-rename candidate is accepted by the DAG-safe lineage rule,
the matching delete/add raw entries SHALL collapse into one `rename` event
retaining old path, new path, canonical blob object ID, and rename status. Its
content counts SHALL be:

```text
canonical_additions = 0
canonical_deletions = 0
canonical_churn     = 0
line_count_status   = exact_rename
```

An exact candidate in an `ambiguous_dag` component SHALL NOT collapse and SHALL
instead produce its ordinary delete/add canonical events under the rules below.

For every other file event, required old/new Git object content SHALL be loaded
from repository objects. If an object required by the analyzed refs is missing or
unreadable, analysis SHALL fail closed rather than invent zero evidence.

A participating side that is absent because the event is an add or delete SHALL
be represented by the empty byte sequence. Gitlink/tree/non-blob entries, or an
event for which line semantics are structurally not applicable, SHALL have:

```text
canonical_additions = 0
canonical_deletions = 0
line_count_status   = binary_or_unavailable
```

For blob-to-blob, empty-to-blob, or blob-to-empty events, if either non-empty blob
contains byte `0x00`, the event SHALL also use `binary_or_unavailable` and zero
canonical additions/deletions. Implementations SHALL NOT substitute byte counts,
estimated lines, textconv output, external-diff output, or backend sentinels.

Otherwise the event SHALL be canonical text evidence computed over raw blob bytes,
without decoding file contents to Unicode:

1. `Lines(bytes)` splits on byte `0x0A` (LF).
2. LF terminates a line and is not part of the line payload.
3. `0x0D` (CR) and every other byte remain part of the line payload.
4. Empty byte sequence has zero lines.
5. A final segment after the last LF is a line only when bytes remain after that LF; a terminal LF does not create an additional trailing line.
6. Line equality is exact byte-sequence equality.
7. Let `L` be the mathematical length of a longest common subsequence of the old and new line sequences.

Canonical counts SHALL be:

```text
canonical_deletions = old_line_count - L
canonical_additions = new_line_count - L
line_count_status   = text
```

The LCS length is unique even when multiple LCS alignments exist, so these totals
SHALL NOT depend on diff-algorithm tie-breaking, Git diff heuristics, attributes,
textconv, external diff, or backend implementation.

If multiple raw entries from one commit map to the same logical file after
canonical identity construction, the analyzer SHALL emit one canonical file
commit touch and aggregate canonical content counts only after accepted exact-
rename collapse.

`commit_count(f)` SHALL equal the number of distinct canonical file-evidence
commits touching logical file `f`, not raw delta-entry count.

```text
churn(f) = Σ(canonical_additions(event) + canonical_deletions(event))
           over canonical file events for f
```

Churn is change volume, not complexity. Exact-rename zero churn,
binary/unavailable zero line churn, and same-path delete/recreate aggregation are
deliberate v1 limitations and SHALL be visible in report interpretation notes.

#### Scenario: Pure exact rename has zero churn
- **WHEN** a 100-line file moves by an accepted exact-blob rename with no content change
- **THEN** the logical file receives one commit touch and zero additions, deletions, and churn

#### Scenario: Ambiguous rename uses ordinary events
- **WHEN** a local exact candidate belongs to an `ambiguous_dag` component
- **THEN** its delete/add entries remain separate events and do not receive exact-rename zero-churn treatment

#### Scenario: Deterministic text line counts
- **WHEN** two text blobs can be aligned by several equally valid diff scripts
- **THEN** additions/deletions derive only from old/new line counts and mathematical LCS length, producing identical totals across implementations

#### Scenario: NUL-containing blob
- **WHEN** either non-empty participating blob contains byte `0x00`
- **THEN** additions/deletions are zero and status is `binary_or_unavailable`

#### Scenario: Missing required blob object
- **WHEN** a required blob object cannot be loaded from the repository object database
- **THEN** analysis fails closed instead of treating the content as zero churn

#### Scenario: Commit count is commit-distinct
- **WHEN** raw normalization yields several entries for the same logical file in one canonical file-evidence commit
- **THEN** `commit_count` increases by exactly one

### Requirement: Path classification
Each logical file SHALL have one primary category derived from its canonical path.
Canonical category order SHALL be:

1. `production`
2. `tests`
3. `docs`
4. `generated`
5. `build_ci`
6. `samples_examples`
7. `unknown`

Alias classifications MAY remain evidence but SHALL NOT replace primary category
or ranking path. #237 owns schema-backed bounded matching, ignores, category
overrides, thresholds, and effective configuration. `unknown` remains visible.

#### Scenario: Rename across categories
- **WHEN** an accepted exact logical-file chain ends at `tests/New.cs`
- **THEN** primary category derives from `tests/New.cs`, not an earlier alias

### Requirement: Total normalization, canonical numbers, and populations
This requirement SHALL define total cohort normalization and canonical numeric values.
For any non-negative population:

```text
normalized(x) = 0                              when max(population) = 0
                x / max(population)            otherwise

normalized_log(x) = 0                          when max(population) = 0
                    log(1+x) / log(1+max)      otherwise
```

Empty/all-zero populations SHALL produce finite zero. Missing optional evidence
SHALL contribute raw zero. Runtime weight renormalization SHALL NOT occur.

Canonical derived real values SHALL use:

```text
Q(v) = round-half-to-even(v, 9 decimal places)
```

Every normalized component, temporal proximity, combined edge weight, final
score, and numeric threshold SHALL be reduced to `Q(v)` before threshold
comparison, ranking, or canonical serialization. Mathematical formulas are
authoritative; implementations MAY use any internal algorithm that produces the
same correctly rounded result.

#237 analysis ignores SHALL remove logical files before score populations and
`G0` construction. Presentation-only suppression SHALL NOT change canonical
scores. File-level populations contain retained logical files in the same primary
category. Edge populations contain `G0` edges in the same unordered endpoint-
category cohort.

#### Scenario: Category isolation
- **WHEN** generated churn is much larger than production churn
- **THEN** it does not set the production normalization maximum

#### Scenario: All-zero population
- **WHEN** every raw value in a component population is zero
- **THEN** every normalized component is canonical zero rather than NaN/Infinity

### Requirement: Valid effective scoring configuration
Each run SHALL have one validated effective scoring configuration. Initial
default profiles are:

- hotspot: commit `.30`, churn `.25`, task `.25`, author `.10`, temporal `.10`;
- bottleneck: independent task `.35`, author `.15`, temporal `.20`, degree `.20`, centrality `.10`;
- OCP: independent task `.40`, centrality `.25`, repeated edit `.25`, role hint `.10`;
- co-change: commit `.75`, task `.25`.

Each configured weight SHALL be a finite non-negative ordinary base-10 decimal
with at most nine fractional digits. Exponent-form authoring is not canonical.
Positive weight means enabled; zero means disabled; at least one component SHALL
be enabled; every exact profile SHALL sum to `1.000000000`. Co-change therefore
requires `alpha + beta = 1.000000000`.

Validation SHALL occur before `Q`; invalid profiles SHALL fail instead of being
rounded, rescaled, or normalized. Evidence absence SHALL NOT change weights or
enabledness.

#### Scenario: Invalid profile sum
- **WHEN** effective weights do not sum exactly to `1.000000000`
- **THEN** validation fails and analysis does not repair the profile

#### Scenario: Missing task evidence
- **WHEN** task evidence is absent but task weight is positive
- **THEN** the task component remains enabled with raw zero and other weights remain unchanged

### Requirement: Deterministic hotspot evidence
This requirement SHALL define deterministic hotspot evidence and scores.
For retained file `f`, using its primary-category population:

```text
C_f = Q(normalized(commit_count(f)))
H_f = Q(normalized_log(churn(f)))
T_f = Q(normalized(distinct_canonical_task_keys(f)))
A_f = Q(normalized(distinct_authors(f)))
R_f = Q(normalized(temporal_span_seconds(f)))

HotspotScore(f) = Q(w_c*C_f + w_h*H_f + w_t*T_f + w_a*A_f + w_r*R_f)
```

`temporal_span_seconds` is the exact integer difference between latest and
earliest canonical file-evidence committer epoch-second values; a one-touch file
has span zero. Findings retain raw metrics, canonical components, effective
weights, primary category, line-count status, and pathname-reuse limitation
status needed to interpret evidence.

Hotspot rankings SHALL be independent per primary category. Production is the
default human-facing top-hotspot group. Cross-category scores SHALL NOT be
interleaved as one numeric ranking.

#### Scenario: Cross-category scores
- **WHEN** docs hotspot score is `0.950000000` and production score is `0.800000000`
- **THEN** the report does not claim the docs file outranks the production file

### Requirement: Canonical base co-change graph
This requirement SHALL define the canonical base co-change graph.
After analysis ignores:

```text
G0 = (V,E0)
V  = retained logical files
E0 = { unordered(a,b) : a != b and CommitCoChange(a,b) > 0 }

CommitCoChange(a,b) = count(canonical file-evidence commits containing both)
TaskCoChange(a,b)   = count(distinct canonical TaskKeys whose file episodes contain both)
```

Task co-change MAY weight an existing `G0` edge but SHALL NOT create an edge when
`CommitCoChange=0`.

For each `G0` edge:

```text
CommitComponent  = Q(normalized(CommitCoChange))
TaskComponent    = Q(normalized(TaskCoChange))
CombinedCoChange = Q(alpha*CommitComponent + beta*TaskComponent)
```

Edge components SHALL normalize within the edge's unordered endpoint-category
cohort. Pair paths SHALL use canonical scalar-value string ordering. Pair
rankings are endpoint-cohort-local.

Distinct-neighbor degree, incident degrees, `IC_f`, `IT_f`, and `K_f` SHALL always
use `G0`, never `Gtheta`. V1 deliberately reuses effective co-change
`alpha/beta` for centrality; there is no second hidden mix.

#### Scenario: Task-only association
- **WHEN** one canonical TaskKey changes A in one commit and B in another but no commit changes both
- **THEN** TaskCoChange may be positive, CommitCoChange is zero, and no `G0` edge exists

#### Scenario: Co-change without tasks
- **WHEN** files change together but no canonical TaskKeys exist
- **THEN** a `G0` edge exists from commit evidence while task evidence is zero and weights remain unchanged

### Requirement: Threshold graph and deterministic clusters
A configured significance threshold SHALL be canonical, lie in `[0,1]`, apply
only to canonical `CombinedCoChange`, and use inclusive `>=`.

```text
Gtheta = (V, { e in E0 : CombinedCoChange(e) >= theta })
```

`Gtheta` SHALL be used only for cluster construction and cluster-derived
candidate logic. Changing `theta` SHALL NOT change `G0`, edge populations, pair
weights/ranking, `D_f`, `IC_f`, `IT_f`, `K_f`, hotspot, bottleneck, or OCP scores.

Clusters are connected components of `Gtheta` with at least two vertices,
constructed independently inside endpoint-category cohorts. Without a threshold,
pair evidence remains and cluster output is empty.

For cluster `C`:

```text
ClusterEdges(C) = { qualifying Gtheta edges whose endpoints are members of C }
ClusterMaximum(C) = max(CombinedCoChange(e) for e in ClusterEdges(C))
ClusterAggregate(C) = Q(sum(CombinedCoChange(e) for e in ClusterEdges(C)))
```

Sub-threshold internal `G0` edges SHALL NOT contribute. Cluster members serialize
in canonical scalar-value path order. Cluster ranking is descending maximum,
descending aggregate, then ascending first member path.

#### Scenario: Threshold equality
- **WHEN** edge weight equals the threshold
- **THEN** the edge qualifies for `Gtheta`

#### Scenario: Threshold does not rescore files
- **WHEN** threshold changes remove all `Gtheta` edges while `G0` is unchanged
- **THEN** `D_f`, `K_f`, bottleneck, and OCP scores remain identical

#### Scenario: Qualifying-edge aggregate
- **WHEN** AB=.600000000, BC=.700000000, AC=.590000000, theta=.600000000
- **THEN** cluster `{A,B,C}` has maximum `.700000000`, aggregate `1.300000000`, and AC contributes nothing

### Requirement: Independent task evidence and temporal proximity
This requirement SHALL define independent task evidence and exact temporal proximity.
A task episode is canonical file-evidence commits linked to one canonical TaskKey.
A multi-reference commit MAY contribute ordinary task breadth and task co-change
but SHALL NOT alone establish independent work.

For file `f`, TaskKeys `x,y` form an independent pair only when each side has at
least one pair-exclusive canonical file-evidence commit touching `f`. Shared-key
commits do not establish independence and do not enter pair-exclusive intervals.

`IndependentTaskSpread(f)` counts canonical TaskKeys participating in at least one
independent pair.

Each pair-side interval is the closed interval of exact arbitrary-precision
committer epoch-second integers from its pair-exclusive commits. For two
intervals, identify earlier and later numerically and define:

```text
gap_seconds = later.start_epoch_second - earlier.end_epoch_second

days_between = 0                         when gap_seconds <= 0
               ceil(gap_seconds / 86400) when gap_seconds > 0

TemporalProximity(x,y) = Q(1 / (1 + days_between))
```

For positive integer gaps, `(gap_seconds + 86399) div 86400` is equivalent.
Calendar dates, local midnight, timezone, DST, bounded host date ranges, and
fractional-day truncation SHALL NOT participate. File temporal value is maximum
canonical pair proximity, or zero when no independent pair exists.

#### Scenario: One multi-reference commit
- **WHEN** the only file-touch commit references canonical TaskKeys `issue#101` and `issue#102`
- **THEN** ordinary task breadth may contain both keys but independent spread and temporal proximity are zero

#### Scenario: Twenty-five hour gap
- **WHEN** pair-exclusive intervals have a positive gap of 90000 seconds
- **THEN** `days_between=2` and proximity is `0.333333333`

### Requirement: Cohort-safe bottleneck centrality and score
This requirement SHALL define cohort-safe bottleneck centrality and scoring.
Using `G0` neighbors:

```text
IncidentCommitDegree(f) = Σ CommitCoChange(f,n)
IncidentTaskDegree(f)   = Σ TaskCoChange(f,n)
IC_f = Q(normalized(IncidentCommitDegree(f))) within f's primary-category cohort
IT_f = Q(normalized(IncidentTaskDegree(f)))   within f's primary-category cohort
K_f  = Q(alpha*IC_f + beta*IT_f)
```

Bottleneck components are:

```text
T_f = Q(normalized(IndependentTaskSpread(f)))
A_f = Q(normalized(distinct_authors(f)))
O_f = Q(normalized(independent_temporal_proximity(f)))
D_f = Q(normalized(distinct_neighbor_degree_G0(f)))
K_f = canonical centrality above

BottleneckScore(f) = Q(b_t*T_f + b_a*A_f + b_o*O_f + b_d*D_f + b_c*K_f)
```

Rankings are primary-category-local. Reports call this parallel-development
bottleneck/pressure and SHALL NOT claim actual merge conflict absent direct
separate evidence.

#### Scenario: Mixed endpoint cohorts
- **WHEN** a production file has production-production and production-tests `G0` edges
- **THEN** centrality uses raw incident evidence normalized in the production file cohort rather than summing incomparable edge-normalized weights

### Requirement: Deterministic OCP-pressure evidence
OCP implementations SHALL use canonical normalized `IndependentTaskSpread` and
the same `G0`-derived `K_f`.

Repeated independent editing is:

```text
Partners_f(t) = { u : canonical TaskKeys (t,u) are independent for f }
PairExclusive_f(t,u) = { canonical commit c touching f : c references t and not u }
Qualifying_f(t) = SHA-deduplicated union of PairExclusive_f(t,u) over Partners_f(t)
Repeated_f(t) = max(|Qualifying_f(t)| - 1, 0)
E_f = sum(Repeated_f(t) for t with Partners_f(t) non-empty)
```

One commit counts at most once per canonical TaskKey after the SHA union, even
when it qualifies against several partners. No independent pair means `E_f=0`.

#### Scenario: Task with multiple partners
- **WHEN** canonical TaskKey `issue#101` is independently paired with `issue#102` and `issue#103`
- **THEN** its pair-exclusive sets are unioned and SHA-deduplicated before repeated edits are counted

### Requirement: Portable deterministic role-token evidence
This requirement SHALL define portable deterministic role-token evidence.
Role hints operate on canonical filename stem using this ASCII tokenizer:

1. any character outside `[A-Za-z0-9]` delimits tokens;
2. split lowercase-letter -> uppercase-letter transitions;
3. split before the final uppercase letter of an uppercase run when the next character is lowercase;
4. split letter <-> digit transitions;
5. map ASCII `A-Z` to `a-z` by ordinal mapping.

Non-ASCII characters are delimiters. Matching uses exact token equality only;
substring, glob, regex, and culture-sensitive matching are forbidden.

Default tokens are `dispatcher`, `registry`, `handler`, `loader`, `session`,
`options`, `configuration`, `command`, `diagnostic`, `mapper`, `dto`, `model`,
`service`, and `orchestrator`. `N_f` is `1.000000000` when any token matches and
zero otherwise. Matched tokens report in canonical scalar-value string order.

```text
OcpPressureScore(f) = Q(o_t*T_f + o_c*K_f + o_r*Q(normalized(E_f)) + o_n*N_f)
```

OCP rankings are category-local. Findings use `OCP pressure` or `likely OCP
violation` with caveats and SHALL NOT claim formal proof.

#### Scenario: Role-token vectors
- **WHEN** stems include `OrderService`, `DiagnosticMapper`, `ViewModel`, `XMLParser2`, `Serviceable`, and `MyDispatcherFactory`
- **THEN** exact token matches include `service`, `diagnostic`, `mapper`, `model`, and `dispatcher`, while `Serviceable` does not match `service`

### Requirement: Stable rankings and refactoring investigations
Within one primary-category cohort, file findings SHALL rank by:

1. descending canonical score;
2. descending ordinary canonical TaskKey spread;
3. descending churn;
4. descending commit count;
5. ascending canonical path by scalar-value ordering.

This total order SHALL apply to hotspots, bottlenecks, and OCP-pressure
findings. Cross-category file findings remain grouped in canonical category
order.

Within one endpoint-category cohort, `G0` pairs rank by descending canonical
combined weight, commit component, task component, then canonical paths. Clusters
use the exact maximum/aggregate/path order above. Cross-cohort pair/cluster
results remain grouped.

Candidates are evidence-derived investigations, not automatic redesign decisions.
They retain source finding IDs, evidence/components, effective thresholds,
category/cohort identity, and caveats. Cluster-derived candidate logic uses
`Gtheta`; file scores remain `G0`-derived.

#### Scenario: Same-cohort total order
- **WHEN** same-cohort file findings tie on all numeric dimensions
- **THEN** canonical scalar-value path ordering is the final discriminator

#### Scenario: OCP score tie uses ordinary file evidence
- **WHEN** two same-category OCP findings have equal canonical score but
  different ordinary canonical TaskKey spread, churn, or commit count
- **THEN** they order by those dimensions before canonical path, even when path
  ordering would produce the opposite result

### Requirement: Canonical JSON string escaping
Every canonical JSON string SHALL contain valid Unicode scalar values. Unpaired
surrogates or otherwise invalid internal Unicode SHALL fail serialization rather
than be replaced.

Escaping SHALL be exactly:

- U+0022 quotation mark => `\"`;
- U+005C reverse solidus => `\\`;
- U+0008/U+0009/U+000A/U+000C/U+000D => `\b`, `\t`, `\n`, `\f`, `\r`;
- other U+0000..U+001F => `\u00XX` with uppercase hexadecimal digits;
- solidus `/` remains unescaped;
- every other Unicode scalar, including non-ASCII, is emitted directly as UTF-8 and SHALL NOT be rewritten as optional `\uXXXX` or surrogate-pair escapes.

This applies equally to property names and string values. Unicode normalization
SHALL NOT be introduced during JSON serialization.

#### Scenario: Escaping vector
- **WHEN** canonical string content contains quote, backslash, slash, U+0001, newline, and `é`
- **THEN** each uses exactly the canonical escape/direct-UTF8 representation above

### Requirement: Successful reports and fail-closed diagnostics
Canonical Markdown/JSON reports SHALL exist only after the analysis has completed
successfully and all fail-closed validation has passed. A failure caused by an
invalid/ambiguous/unsupported ref operand, malformed/unreadable required commit
object, malformed author or committer header, invalid selected author or message
UTF-8, TaskKey extraction ambiguity, invalid Git path UTF-8, missing required
Git object, or invalid configuration SHALL NOT emit a partial canonical report,
partial ranking, or refactoring-candidate set.

Failure diagnostics are a separate command/error surface. They SHALL identify a
stable diagnostic kind and the relevant canonical object identity or source span
when available, but they SHALL NOT be represented as successful canonical report
records. Exact diagnostic rendering/exit-code schema belongs to the implementing
CLI/report tasks and SHALL NOT alter successful report artifact identity.

#### Scenario: Fail-closed analysis produces no report
- **WHEN** a canonical TaskKey extraction collision or invalid UTF-8 commit message causes analysis to fail
- **THEN** no successful canonical Markdown/JSON report or candidate set is emitted

### Requirement: Deterministic report semantics and mandatory canonical provenance
Markdown SHALL contain range/config summary, analyzed/excluded merge counts,
production hotspots, separate non-production groups, co-change cohorts,
bottlenecks, OCP pressure, candidates, and interpretation limits.

Successful canonical JSON SHALL include input/config/history-semantics identity,
repository object-hash format, authored operands and resolved lowercase full
commit IDs, canonical numeric scale, exact canonical committer epoch-second
integers and raw timezone-token evidence, the mandatory ordered hexadecimal
`encoding ` header provenance arrays, canonical author identities, the complete
ordered TaskKey match-provenance collection plus deduplicated canonical TaskKeys,
paths/aliases/categories, the complete ordered local rename-candidate/component
provenance with accepted/`ambiguous_dag` outcome, canonical file events and line-
count status, raw/canonical score components, weights/thresholds, `G0`/cluster
cohort identity, independent-task and centrality evidence, OCP evidence,
enrichment status where available, excluded merge count, and candidates.

Canonical evidence required by the successful report SHALL NOT be optional at an
upstream layer. Additional debug-only evidence MAY exist outside canonical JSON,
but its presence/absence SHALL NOT alter successful canonical artifact identity.

Object properties SHALL serialize in the order declared by #243's versioned
schema. Dynamic object/map keys SHALL use canonical scalar-value string ordering.
Arrays SHALL preserve the category/cohort/ranking/provenance ordering defined by
this capability and #243.

Canonical JSON bytes SHALL use:

- UTF-8 without BOM;
- LF (`\n`) line endings;
- two-space indentation;
- no trailing whitespace;
- exactly one terminal LF;
- canonical JSON escaping above;
- exact lowercase full hexadecimal Git object IDs;
- exact non-exponent decimal integers for raw counts, TaskKey IDs, epoch seconds,
  and gaps;
- exactly nine fractional digits for canonical real values;
- no exponent notation for canonical real values.

Canonical report artifact identity SHALL be over these exact bytes, not semantic
JSON equivalence or in-memory dictionary order.

Optional .NET/Roslyn enrichment is downstream; failure SHALL NOT drop, change,
rescore, or reorder Git-level findings.

Reports SHALL state that churn is not complexity; co-change is not module proof;
task/author evidence may be incomplete; raw non-UTF8 author/message metadata fails
closed in v1; canonical temporal math uses raw committer epoch-second integers and
ignores timezone token for the epoch value; task source spellings are normalized
to canonical TaskKeys; excluded merge file deltas can understate merge-resolution
edits; exact rename misses rename-with-edit; DAG-ambiguous or lifecycle-broken
rename candidates are not collapsed; v1 intentionally aggregates delete/recreate
events at the same pathname into one baseline identity and may therefore conflate
unrelated pathname generations; accepted exact renames contribute zero content
churn; NUL/gitlink/non-line events contribute zero line churn with explicit
status; strict UTF-8 is required for v1 Git paths; normalized scores compare only
inside their cohorts; role hints are bounded heuristics; and people decide
whether to refactor.

#### Scenario: Deterministic rendering
- **WHEN** identical successful canonical evidence is rendered by two conforming implementations
- **THEN** canonical JSON bytes are identical

### Requirement: Contributor theory reference
The repository SHALL contain an internal contributor reference consistent with
this capability, including deterministic ref resolution/object-ID format,
mandatory provenance, raw commit metadata/task identity, precise author and
committer header parsing, exact temporal integer semantics, reachability/merge
semantics, strict UTF-8 path model, baseline same-path identity, scalar-value
string ordering, formal DAG-safe exact rename component/lifecycle semantics,
canonical file-event and LCS line-churn semantics, category/cohort normalization,
numeric/weight rules, `G0/Gtheta`, cluster aggregation, exact epoch-second
temporal proximity, independent-task/repeated-edit semantics, cohort-safe
centrality, ASCII role tokenization, successful-report/failure boundary,
canonical JSON escaping/bytes, reports, ownership, and limitations. Public MkDocs
navigation SHALL NOT advertise the feature before implementation ships.

#### Scenario: Contributor discovers theory
- **WHEN** a contributor opens the internal documentation index
- **THEN** it links to the Release Architecture Forensics theory reference

### Requirement: One bounded history-analysis policy authority
This requirement SHALL retain one bounded history-analysis policy authority.
The architecture policy MAY contain one optional `history_analysis` object. It
SHALL be loaded, composed, provenance-checked, schema-validated, and raw-YAML
validated by the normal policy lifecycle; a history command SHALL NOT accept a
second configuration document or individual semantic tuning switches.

When the object is absent, the effective profile SHALL contain the fixed #235
default weight profiles, no co-change significance threshold, no configured
path/ignore patterns, and the built-in `issue` TaskKey extractor. Supplying a
policy through the history command SHALL use exactly its effective
`history_analysis` object; omitting a policy SHALL use that default profile.

#### Scenario: Absent configuration preserves defaults
- **WHEN** a valid architecture policy does not declare `history_analysis`
- **THEN** policy loading succeeds and history ingestion uses the built-in issue extractor and default effective profile

#### Scenario: Imported configuration has one source of truth
- **WHEN** an imported architecture policy contributes `history_analysis`
- **THEN** composition and validation apply the same policy schema and diagnostics as every other policy section

### Requirement: Bounded configured TaskKey extractors
Each configured extractor SHALL contain a unique stable `id`, a namespace, and
a pattern. IDs and namespaces SHALL each match `[a-z][a-z0-9._-]*`; `issue` is
reserved for the built-in extractor and SHALL NOT be configured. A pattern
SHALL contain a non-empty literal `prefix` and an optional literal `suffix`.
It SHALL match exactly one positive ASCII-decimal identifier in
`prefix + [0-9]+ + suffix`, with the scalar before and after the full match,
when present, outside `[A-Za-z0-9_#]`.

Configured extractors SHALL scan raw message bytes and emit the whole literal
match as a non-empty half-open byte span. They SHALL pass matches to the
existing canonical TaskKey extraction stage, which alone owns strict UTF-8,
arbitrary-precision normalization, ordering, deduplication, and overlapping
different-key failure. Extractor declaration order SHALL NOT select a result.

#### Scenario: Custom literal extractor preserves provenance
- **WHEN** an extractor with ID `jira`, namespace `jira`, and prefix `JIRA-` examines `fix JIRA-001`
- **THEN** it emits the complete `JIRA-001` byte span with canonical TaskKey `(jira,1)`

#### Scenario: Reserved default semantics cannot be replaced
- **WHEN** a policy configures an extractor whose ID is `issue`
- **THEN** policy loading fails rather than replacing the built-in #235 boundary semantics

#### Scenario: Custom extractor collision fails closed
- **WHEN** the built-in or configured extractors produce overlapping spans for different TaskKeys
- **THEN** canonical extraction fails with the existing ambiguity diagnostic regardless of declaration order

### Requirement: Deterministic path categories and analysis ignores
This requirement SHALL define deterministic path categories and analysis ignores.
`history_analysis.paths` MAY configure segment-glob pattern lists for
`production`, `tests`, `docs`, `generated`, `build_ci`, and `samples_examples`.
A glob SHALL use `/`-separated literal segments, `*` as one segment, and `**`
as zero or more segments. Backslashes, empty/dot segments, partial wildcards,
and character classes SHALL be rejected. Matching SHALL compare the exact
strict-UTF8 canonical path scalar sequence without normalization, collation, or
filesystem conversion.

`history_analysis.ignore` MAY contain the same glob grammar. An ignored path
SHALL be removed before category populations, `G0`, and every downstream score;
presentation options SHALL NOT rescore it. A retained path SHALL select the
first matching configured category in fixed order `production`, `tests`,
`docs`, `generated`, `build_ci`, `samples_examples`, or SHALL be `unknown`.

#### Scenario: Exact distinct Unicode paths remain distinct
- **WHEN** configured patterns match one of two scalar-distinct NFC/NFD path spellings
- **THEN** only that exact spelling matches and the other path retains its independently determined category

#### Scenario: Ignore precedes classification
- **WHEN** a path matches both an ignore pattern and a production pattern
- **THEN** it is removed before category and score population construction

### Requirement: Exact validated analysis profiles and threshold
This requirement SHALL define exact validated analysis profiles and thresholds.
`history_analysis.weights` MAY explicitly set complete hotspot, co-change,
bottleneck, and OCP profiles. Each value SHALL be a finite nonnegative ordinary
base-10 decimal literal with at most nine fractional digits; a positive value
enables its component and zero disables it. Every profile SHALL have at least
one enabled component and an exact sum of `1.000000000`; co-change alpha and
beta SHALL have that exact sum. Invalid input SHALL fail policy validation before
any quantization, analysis, or report construction and SHALL NOT be repaired.

`history_analysis.thresholds.co_change_significance`, when present, SHALL be a
canonical decimal in `[0,1]`. It SHALL be consumed only as the inclusive
`CombinedCoChange >= threshold` gate for later `Gtheta` clusters and candidate
construction, never as a score or `G0` control.

#### Scenario: Invalid profile fails before analysis
- **WHEN** a configured hotspot profile sums to `0.999999999` or has more than nine fractional digits
- **THEN** policy validation fails and no analysis starts

#### Scenario: Threshold equality qualifies only for Gtheta
- **WHEN** a future combined co-change edge equals the configured threshold
- **THEN** it qualifies for `Gtheta` without changing `G0` or file-score inputs

### Requirement: Deterministic in-memory hotspot findings
The Core history-analysis layer SHALL expose a deterministic in-memory hotspot
analysis result for a successful canonical ingestion result and its validated
effective `history_analysis` configuration. Each retained logical-file finding
SHALL retain canonical path, primary category, raw commit/churn/TaskKey/author/
temporal evidence, nine-decimal canonical components and score, effective
hotspot weights, line-count statuses, and the inherited pathname-reuse
limitation status. The analysis layer SHALL consume only canonical ingestion
evidence and SHALL NOT re-resolve refs, re-decode metadata, re-extract task keys,
segment same-path lifetimes, re-evaluate rename candidates, or use host
date/time conversion.

#### Scenario: Canonical evidence is independent of source spellings
- **WHEN** canonical evidence contains task spellings `#001` and `#1`, or equal
  committer epoch integers with distinct timezone tokens
- **THEN** hotspot task breadth and temporal span consume the canonical TaskKey
  and exact epoch integer without observing those source spelling/token variants

### Requirement: Cohort-safe hotspot result ordering
The hotspot analysis layer SHALL apply configured history ignores before
classification, normalization, and scoring. It SHALL normalize each hotspot
component only among retained files in the same primary category and SHALL group
results in canonical category order with production as the first human-facing
group. Within one category it SHALL order findings by descending canonical score,
descending canonical TaskKey spread, descending churn, descending commit count,
and ascending scalar-value canonical path.

#### Scenario: Non-production cannot change production scores
- **WHEN** a generated file has more churn than every production file
- **THEN** the generated file does not affect any production hotspot component,
  score, or production-group ranking

### Requirement: Deterministic bottleneck analysis projection
The system SHALL project finalized canonical file, TaskKey, author, and `G0` evidence into category-local parallel-development bottleneck findings. Each finding SHALL expose canonical TaskKey pairs and ordered source provenance, pair-exclusive exact epoch-second intervals and gaps, canonical authors, raw and normalized components, effective weights, logical-file aliases, and the pathname-reuse limitation. The projection SHALL consume neither source spellings nor `Gtheta` topology and SHALL describe pressure rather than an actual merge conflict.

#### Scenario: Pair-exclusive independence
- **WHEN** a file has one canonical file-evidence commit containing `issue#101` but not `issue#102` and another containing `issue#102` but not `issue#101`
- **THEN** both TaskKeys participate in one independent pair and their pair-exclusive intervals determine temporal proximity

#### Scenario: Shared commit does not establish a pair
- **WHEN** the only file-evidence commit contains canonical `issue#101` and `issue#102`
- **THEN** the finding has ordinary task breadth but zero independent-task spread and zero temporal proximity

#### Scenario: G0-only centrality
- **WHEN** the significance threshold changes while raw `G0` edges are unchanged
- **THEN** distinct-neighbor degree, centrality, and bottleneck score remain identical

#### Scenario: Exact temporal evidence
- **WHEN** pair-exclusive intervals have a 90,000-second positive gap, including epochs outside host date ranges or equal epochs with different timezone tokens
- **THEN** the gap has `days_between=2`, temporal proximity `0.333333333`, and endpoints are selected solely from epoch integers

### Requirement: Auditable OCP-pressure finding projection
Release Architecture Forensics SHALL project one OCP-pressure finding for every
retained, non-ignored canonical logical file, grouped and ranked only within its
primary path category. Each finding SHALL expose the canonical path and aliases,
pathname-reuse limitation, canonical TaskKeys, pair-exclusive TaskKey-pair
provenance, SHA-deduplicated qualifying commit IDs per participating TaskKey,
repeated-edit total, matched ASCII role tokens, raw `G0` incident degree
evidence, normalized score components, and validated effective OCP weights.
Canonical JSON SHALL include the OCP finding groups and this evidence after all
fail-closed ingestion validation succeeds.

Findings SHALL describe heuristic `OCP pressure` or a `likely OCP violation`
with caveats and SHALL NOT claim a formal design-principle proof. Missing task,
role, or graph evidence SHALL contribute canonical zero without reweighting any
enabled OCP component.

#### Scenario: Ignored logical file
- **WHEN** analysis ignores remove a canonical logical file before `G0` and
  score-population construction
- **THEN** no OCP-pressure finding is projected for that file

#### Scenario: Missing OCP evidence
- **WHEN** a retained canonical logical file has no independent TaskKey pair, no
  matching role token, and no `G0` incident edge
- **THEN** its repeated-edit, role-hint, and centrality components are all
  `0.000000000`, the configured weights are unchanged, and the finding remains
  available as a caveated OCP-pressure result

#### Scenario: Auditable multi-partner repeated editing
- **WHEN** one canonical TaskKey has independent pairs with multiple partners
  and one qualifying commit is pair-exclusive against more than one partner
- **THEN** the finding records that commit once for the TaskKey and computes its
  repeated-edit contribution from the SHA-deduplicated union

### Requirement: Enrichment is a non-authoritative downstream projection
Optional .NET/Roslyn enrichment SHALL execute only after canonical Git-level
analysis has completed. Enabling, disabling, succeeding, or failing enrichment
SHALL NOT change canonical ref/metadata/TaskKey/path/rename/temporal/graph
evidence, finding identity, score, rank, candidate eligibility, or candidate
ordering. A valid Git-only result SHALL remain reportable without enrichment.

#### Scenario: Enrichment failure preserves canonical evidence
- **WHEN** a completed canonical Git analysis cannot obtain .NET enrichment
- **THEN** the same findings, provenance, scores, ranks, and ordering are retained with only the enrichment projection/status differing

#### Scenario: Enrichment cannot repair path identity
- **WHEN** finalized Git evidence contains same-path reuse or an `ambiguous_dag` rename component
- **THEN** enrichment does not split, merge, or otherwise modify the finalized logical-file identity

### Requirement: Release-closure dogfood and conformance evidence
The release process SHALL retain deterministic dogfood and conformance evidence before the story closes.

Before a Release Architecture Forensics v1 release story closes, the repository
SHALL retain a repository-safe deterministic dogfood summary for at least one
real ArchLinterNet release range. The summary SHALL record separate authored
`from` and `to` operands, their resolved canonical object IDs, effective
history-analysis configuration/profile identity, tool version and source
revision, canonical JSON artifact identities, selected finding/candidate
identities, comparison with known manual observations, documented intentional
v1 false-positive/false-negative limitations, tuning evidence when applicable,
and enrichment status.

The dogfood command SHALL use separate `--from` and `--to` operands; it SHALL
NOT use a Git revision-expression range as one operand. The release conformance
suite SHALL retain focused vectors proving that canonical JSON is invariant under
environment presentation variation and that available or unavailable optional
.NET enrichment changes only the reserved enrichment projection, never Git-level
evidence, findings, scores, ranks, or candidate ordering.

Canonical file-identity, rename-lineage, and churn construction SHALL remain
separate from heuristic scoring. Dogfood evidence SHALL NOT be used to retune or
otherwise change finalized canonical evidence semantics within the release-story
scope. If a dogfood run establishes a need to change those semantics, the change
SHALL be proposed as separate reviewed specification and migration work before
implementation.

#### Scenario: Historical range has unavailable enrichment
- **WHEN** a real historical release range is analyzed with requested .NET
  enrichment from a worktree whose checked-out `HEAD` differs from resolved `to`
- **THEN** the command produces a successful Git-only report with an explicit
  `unavailable` enrichment status and the same Git-level canonical output as
  the corresponding run without requested enrichment

#### Scenario: Environment-varied canonical report
- **WHEN** identical finalized Git evidence and configuration are rendered in
  different presentation environments
- **THEN** the canonical JSON bytes before the reserved enrichment projection
  are identical and retain the same findings, scores, ranks, and candidates

#### Scenario: Dogfood reveals a canonical-evidence semantic change
- **WHEN** release dogfooding shows that canonical file identity, rename lineage,
  or churn semantics need to change
- **THEN** the release story records the observation without retuning those
  semantics and a separate reviewed specification/migration change is required
  before implementing it
