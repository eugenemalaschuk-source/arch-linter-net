## ADDED Requirements

### Requirement: Explicit deterministic analysis identity and object IDs
Release Architecture Forensics SHALL analyze explicit exclusive-`from`,
inclusive-`to` operands that resolve to commits before analysis. Canonical
identity SHALL include authored operands, resolved full commit IDs, repository
object-hash format, effective `history_analysis` config identity,
history-semantics profile identity, and tool version while excluding local
environment presentation data.

Canonical Git object IDs SHALL use the repository hash format and render the full
digest as lowercase hexadecimal, two digits per byte. Abbreviated IDs are not
canonical v1 operands.

### Requirement: Deterministic ref resolution and tag peeling
V1 authored operands SHALL resolve only as literal `HEAD`, full-length object ID,
exact fully-qualified `refs/...`, or shorthand searched exactly as
`refs/tags/<operand>` and `refs/heads/<operand>`. A tag/head shorthand collision
SHALL fail ambiguous. Symbolic refs SHALL dereference with cycle detection;
annotated tags SHALL peel recursively until the final object, which MUST be a
commit. Revision-expression grammar, reflog selectors, `~`/`^`, `^{...}`, path
suffixes, and abbreviated object IDs SHALL NOT be interpreted.

### Requirement: Raw commit metadata and canonical author identity
Canonical author/task/time evidence SHALL be parsed from raw Git commit-object
bytes, not presentation APIs that can transcode, normalize, locale-decode, or
calendar-convert metadata. Required raw commit structure SHALL parse or analysis
fails closed.

Exactly one canonical `author` header SHALL parse by the right-to-left suffix
`<identity> SP <timestamp> SP <timezone>`, with timestamp `-?[0-9]+`, timezone
`[+-][0-9]{4}`, and final angle-bracketed email delimited by the last `<` and
final `>`. Malformed/non-unique structure SHALL fail closed.

Canonical author SHALL select non-empty ASCII-SP/HT-trimmed email else name,
strict-UTF8 decode the selected bytes, trim ASCII SP/HT, lowercase ASCII `A-Z`
only, perform no Unicode normalization/case folding, and use `unknown` only when
email/name are both empty.

Every direct `encoding ` header SHALL be mandatory canonical provenance as its raw
value bytes encoded lowercase hexadecimal in original header order. It SHALL NOT
trigger canonical transcoding.

### Requirement: Canonical committer epoch-second integer
Exactly one canonical `committer` header SHALL use the same suffix grammar. Its
timestamp token SHALL be an arbitrary-precision signed base-10 Unix epoch-second
integer. The timezone token SHALL be validated/retained as metadata but SHALL NOT
shift the epoch value. Canonical commit order and temporal math SHALL use the exact
integer, not wall-clock conversion, floating seconds, or a host date/time range.

### Requirement: Canonical TaskKey identity, boundaries, and provenance
Raw commit-message payload SHALL strict-UTF8 decode before task extraction;
invalid UTF-8 SHALL fail closed.

Every #237 extractor SHALL have a stable ASCII-lowercase extractor ID and map one
match to an ASCII-lowercase namespace plus one positive ASCII-decimal ID:

```text
TaskKey = (namespace, positive_decimal_id)
```

IDs are arbitrary precision and render without leading zeroes. Every match SHALL
retain mandatory canonical provenance containing extractor ID, TaskKey, non-empty
half-open raw-message byte span, and exactly matched decoded UTF-8 substring.
Identical provenance records deduplicate and order by span, extractor ID, and
TaskKey. Overlapping spans mapping to different TaskKeys SHALL fail closed.

The default extractor ID/namespace is `issue`; it matches literal `#` plus one or
more ASCII digits with numeric value > 0 only when the scalar before `#` and the
scalar after the final digit, when present, are both outside `[A-Za-z0-9_#]`.
Thus `#001` and `#1` are one `(issue,1)`, while `#0`, `abc#12`, `#12foo`, `##12`,
and `#12#13` do not match.

### Requirement: Canonical commit set and file evidence
The analyzed commit set SHALL be:

```text
Commits(from,to) = Reachable(to) \ Reachable(from)
```

Commits SHALL sort by exact canonical committer epoch-second integer then full
canonical commit ID. One-parent commits SHALL use parent-tree -> commit-tree
deltas; roots SHALL compare with the empty tree. Merge commits SHALL remain
metadata-only and SHALL NOT contribute file-derived evidence in the initial
profile.

### Requirement: Canonical Git path text and ordering
Every Git path entering canonical evidence SHALL decode as strict UTF-8.
Ill-formed UTF-8 SHALL fail analysis closed. Locale/code-page fallback,
replacement decoding, and Unicode normalization SHALL NOT participate.

Canonical ordinal string order SHALL be lexicographic by Unicode scalar numeric
value, prefix-shorter-first. It SHALL NOT depend on UTF-16 code-unit ordering,
locale/filesystem collation, or normalization libraries.

### Requirement: Baseline same-path identity
Before accepted rename unions, all canonical non-merge events with the same exact
canonical repository path string SHALL belong to one baseline path identity for
the entire analyzed commit set. V1 SHALL NOT split that identity across deletion/
re-addition, unrelated blob replacement, or reachable branches. This deliberate
pathname-reuse conflation SHALL be reported as a limitation. Lifetime segmentation
requires a future semantic-profile change.

### Requirement: Formal DAG/lifecycle-safe exact rename identity
V1 SHALL create a local exact-rename candidate only for a same-commit one-to-one
delete/add relation with identical Git blob identity and no competing source or
destination. Similarity/copy inference and rename-with-edit SHALL NOT create
candidates.

For candidate `c`, define `Endpoints(c)={src(c),dst(c)}`. Build an undirected
candidate-overlap graph whose vertices are candidates and whose edges connect
candidates exactly when endpoint sets intersect. Its connected components are
potential lineage components.

A component SHALL collapse only when exactly one permutation uses all candidates,
every earlier candidate commit is a strict Git ancestor of every later candidate
commit, each destination equals the next source, and no ordinary canonical add/
delete of the shared path occurs in a non-merge commit strictly between adjacent
candidates. Such an intervening add/delete is a lifecycle break.

An ancestry-incomparable fork/join, non-unique sequence, or lifecycle break SHALL
produce `ambiguous_dag`; none of its candidates collapse identity. Timestamp/ID
ordering SHALL NOT repair DAG/path-lifecycle ambiguity. Accepted lineages union
baseline path identities; ambiguous components perform no cross-path union.

Every local candidate SHALL be mandatory canonical provenance with commit ID,
source, destination, blob ID, component membership/status, and accepted/ambiguous
outcome. Candidate/component ordering SHALL be deterministic by canonical commit
order, paths, blob ID, and minimum candidate key.

### Requirement: Canonical file events and line churn
There SHALL be one canonical file event per logical file per canonical
file-evidence commit.

An accepted exact rename SHALL collapse its raw delete/add pair into one touch:

```text
canonical_additions = 0
canonical_deletions = 0
canonical_churn     = 0
line_count_status   = exact_rename
```

A candidate in `ambiguous_dag` SHALL NOT collapse; its delete/add entries remain
ordinary events.

For other events, required repository objects SHALL be loadable or analysis fails
closed. Missing add/delete side SHALL be empty bytes. Gitlinks/non-blob/non-line
events and any event whose non-empty participating blob contains NUL (`0x00`)
SHALL use zero line counts plus `binary_or_unavailable`; byte counts, estimates,
textconv, external diff, or backend sentinels SHALL NOT substitute.

Otherwise line sequences SHALL be built directly from raw blob bytes by splitting
on LF (`0x0A`), preserving CR/other bytes as payload, treating terminal LF as no
extra trailing line, and comparing lines by exact byte equality. Let `L` be the
mathematical LCS length:

```text
canonical_deletions = old_line_count - L
canonical_additions = new_line_count - L
line_count_status   = text
```

`commit_count(f)` SHALL count distinct canonical file-evidence commits, not raw
delta entries. Churn SHALL sum canonical additions plus deletions over canonical
file events.

### Requirement: Categories, populations, canonical numbers, and weights
Primary category SHALL derive from canonical path in fixed order `production`,
`tests`, `docs`, `generated`, `build_ci`, `samples_examples`, `unknown`.
#237 ignores SHALL apply before score populations and `G0` construction. File
metrics normalize inside primary-category cohorts; edge metrics inside unordered
endpoint-category cohorts.

Canonical derived reals SHALL use
`Q(v) = round-half-to-even(v, 9 decimal places)` before threshold comparison,
ranking, or serialization. Missing optional evidence SHALL contribute zero and
weights SHALL NOT be implicitly renormalized.

Weights SHALL be finite non-negative ordinary base-10 decimals with at most nine
fractional digits. Positive means enabled, zero disabled, at least one component
is enabled, and each exact profile SHALL sum to `1.000000000`; co-change requires
`alpha + beta = 1.000000000`.

### Requirement: Deterministic hotspot evidence
Hotspot SHALL consume canonical commit count, LCS churn, canonical TaskKey spread,
canonical author spread, and exact committer epoch-second span with effective
weights. Rankings SHALL be primary-category-local.

### Requirement: Canonical base graph, threshold graph, and clusters
`G0` SHALL contain retained logical files and exactly unordered pairs with
`CommitCoChange>0`. `TaskCoChange` counts canonical TaskKeys. Task evidence MAY
weight an existing edge but SHALL NOT create topology when commit co-change is
zero. Pair normalization/ranking, degree, incident evidence, and `K_f` use `G0`.

A configured threshold SHALL apply only to canonical `CombinedCoChange` with
inclusive `>=`. `Gtheta` SHALL affect only clusters and cluster-derived candidates
and SHALL NOT alter `G0`, pair normalization/ranking, `D_f`, `K_f`, or file scores.
`ClusterMaximum` and `ClusterAggregate` use qualifying `Gtheta` edges only.

### Requirement: Independent TaskKey evidence, temporal proximity, and OCP
A multi-reference commit may contribute ordinary canonical TaskKey breadth/co-
change but SHALL NOT establish independent work alone. Independent pairs require
pair-exclusive canonical file-evidence commits on both sides.

Pair intervals SHALL be closed intervals over exact committer epoch integers:

```text
gap_seconds = later.start_epoch_second - earlier.end_epoch_second
days_between = 0                         when gap_seconds <= 0
               ceil(gap_seconds / 86400) when gap_seconds > 0
TemporalProximity = Q(1/(1+days_between))
```

Calendar dates, timezone, local midnight, host date ranges, and DST SHALL NOT
participate. Centrality uses raw `G0` incident commit/task degrees normalized in
the file cohort. Repeated OCP editing unions pair-exclusive commits per canonical
TaskKey across partners, deduplicates by SHA, then counts repeats. Role hints use
the fixed ASCII tokenizer and exact token equality only.

### Requirement: Successful reports and fail-closed diagnostics
Canonical Markdown/JSON SHALL be emitted only after all fail-closed validation
succeeds. Invalid refs, commit metadata/UTF-8/config, TaskKey overlap ambiguity,
missing required objects, or other canonical failures SHALL emit no partial
successful report, ranking, or candidate set. Failure diagnostics are a separate
command/error surface and SHALL NOT be successful report records.

### Requirement: Mandatory canonical provenance and canonical JSON bytes
Successful canonical JSON SHALL include repository object-hash format, authored
operands/resolved full commit IDs, exact committer epoch/timezone evidence,
ordered hexadecimal `encoding ` header provenance, canonical authors, complete
ordered TaskKey match provenance plus deduplicated TaskKeys, complete ordered
rename-candidate/component provenance, paths/aliases/categories, file events,
score/graph/OCP evidence, enrichment status, and candidates.

Evidence required by successful canonical JSON SHALL NOT be optional upstream.
Additional debug-only evidence MAY exist outside canonical JSON but SHALL NOT
alter canonical artifact identity.

Canonical strings SHALL contain valid Unicode scalar values and SHALL NOT be
Unicode-normalized during serialization. JSON escaping SHALL use `\"` for quote,
`\\` for backslash, standard short escapes for backspace/tab/newline/formfeed/
carriage-return, uppercase `\u00XX` for other C0 controls, literal `/`, and direct
UTF-8 for every other scalar.

Object properties follow #243's versioned schema order; dynamic map keys use
canonical scalar-value string order. Canonical bytes use UTF-8 without BOM, LF,
two-space indentation, no trailing whitespace, exactly one terminal LF, full
lowercase Git IDs, exact non-exponent integers for raw integer fields, and fixed
nine-decimal non-exponent canonical reals. Report identity is over exact bytes.

### Requirement: Contributor reference
The internal contributor reference SHALL remain synchronized with the capability,
including ref resolution/object IDs, exact author/committer parsing, TaskKey
identity/boundaries/provenance, strict UTF-8/scalar ordering, baseline same-path
identity, formal DAG/lifecycle-safe rename, raw-byte LCS churn, cohort/numeric
rules, `G0/Gtheta`, temporal/OCP semantics, report/failure boundary, mandatory
provenance, portable role tokens, canonical JSON bytes, and limitations. Public
MkDocs navigation SHALL not advertise the feature before it ships.
