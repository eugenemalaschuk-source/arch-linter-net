## ADDED Requirements

### Requirement: Explicit deterministic analysis identity
Release Architecture Forensics SHALL analyze explicit exclusive-`from`,
inclusive-`to` refs that resolve before analysis. Canonical identity SHALL include
authored/resolved refs, effective `history_analysis` config identity,
history-semantics profile identity, and tool version while excluding local
environment presentation data.

### Requirement: Raw commit metadata and canonical author identity
Canonical author/task/time evidence SHALL be parsed from raw Git commit-object
bytes, not presentation APIs that can transcode, normalize, locale-decode, or
calendar-convert metadata. Required raw commit structure SHALL parse or analysis
fails closed.

Exactly one canonical `author` header SHALL use the right-to-left suffix grammar
`<identity> SP <timestamp> SP <timezone>`, with timestamp `-?[0-9]+`, timezone
`[+-][0-9]{4}`, and final angle-bracketed email delimited by the last `<` and final
`>`. Malformed/non-unique structure SHALL fail closed.

Canonical author SHALL select non-empty ASCII-SP/HT-trimmed email else name,
strict-UTF8 decode the selected bytes, trim ASCII SP/HT, lowercase ASCII `A-Z`
only, perform no Unicode normalization/case folding, and use `unknown` only when
email/name are both empty. A Git `encoding` header SHALL NOT trigger canonical
transcoding.

### Requirement: Canonical committer epoch-second integer
Exactly one canonical `committer` header SHALL use the same suffix grammar. Its
timestamp token SHALL be an arbitrary-precision signed base-10 Unix epoch-second
integer. The timezone token SHALL be validated/retained as metadata but SHALL NOT
shift the epoch value. Canonical commit order and temporal math SHALL use the exact
integer, not wall-clock conversion, floating seconds, or a host date/time range.
Malformed/missing/duplicate committer headers SHALL fail closed.

### Requirement: Canonical TaskKey identity
Raw commit-message payload SHALL strict-UTF8 decode before task extraction;
invalid UTF-8 SHALL fail closed.

Every #237 extractor SHALL map one match to an ASCII-lowercase namespace
`[a-z][a-z0-9._-]*` and one positive ASCII-decimal ID. Canonical task identity is:

```text
TaskKey = (namespace, positive_decimal_id)
```

IDs are arbitrary precision, greater than zero, and render without leading zeroes.
`#0` produces no default TaskKey. Extractor matches expose non-empty half-open raw
message byte spans. Same-key matches deduplicate; overlapping spans mapping to
different TaskKeys fail closed. Non-overlapping distinct matches may remain
distinct. All task-derived metrics SHALL consume TaskKeys, not source spellings.

### Requirement: Canonical commit set and file evidence
The analyzed commit set SHALL be:

```text
Commits(from,to) = Reachable(to) \ Reachable(from)
```

Commits SHALL sort by exact canonical committer epoch-second integer then full
SHA. One-parent commits SHALL use parent-tree -> commit-tree deltas; roots SHALL
compare with the empty tree. Merge commits SHALL remain metadata-only and SHALL
NOT contribute file-derived evidence in the initial profile.

### Requirement: Canonical Git path text and ordering
Every Git path entering canonical evidence SHALL decode as strict UTF-8.
Ill-formed UTF-8 SHALL fail analysis closed. Locale/code-page fallback,
replacement decoding, and Unicode normalization SHALL NOT participate.

Canonical ordinal string order SHALL be lexicographic by Unicode scalar numeric
value, prefix-shorter-first. It SHALL NOT depend on UTF-16 code-unit ordering,
locale/filesystem collation, or normalization libraries.

### Requirement: Formal DAG-safe exact rename identity
V1 SHALL create a local exact-rename candidate only for a same-commit one-to-one
delete/add relation with identical Git blob identity and no competing source or
destination. Similarity/copy inference and rename-with-edit SHALL NOT create
candidates.

For each candidate `c`, define `Endpoints(c)={src(c),dst(c)}`. Build an undirected
candidate-overlap graph whose vertices are candidates and whose edges connect
candidates exactly when endpoint sets intersect. Its connected components are the
potential lineage components.

A component SHALL collapse only when exactly one permutation uses all candidates
and all three hold: every earlier candidate commit is a strict ancestor of every
later candidate commit; each destination equals the next source; and no ordinary
canonical add/delete of the shared path occurs in a non-merge commit strictly
between adjacent candidate commits. Such an intervening add/delete is a lifecycle
break.

An ancestry-incomparable fork/join, non-unique sequence, or lifecycle break SHALL
produce `ambiguous_dag`; none of its candidates collapse identity. Timestamp/SHA
ordering SHALL NOT repair DAG/path-lifecycle ambiguity. Accepted lineages use the
final in-range canonical path and deterministic aliases; ambiguous candidates
remain evidence while involved paths stay separate.

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

### Requirement: Categories, populations, and canonical numbers
Primary category SHALL derive from canonical path in fixed order `production`,
`tests`, `docs`, `generated`, `build_ci`, `samples_examples`, `unknown`.
#237 ignores SHALL apply before score populations and `G0` construction.
File metrics normalize inside primary-category cohorts; edge metrics inside
unordered endpoint-category cohorts.

Canonical derived reals SHALL use
`Q(v) = round-half-to-even(v, 9 decimal places)` before threshold comparison,
ranking, or serialization. Missing optional evidence SHALL contribute zero and
weights SHALL NOT be implicitly renormalized.

### Requirement: Valid effective weights
Weights SHALL be finite non-negative ordinary base-10 decimals with at most nine
fractional digits. Positive means enabled, zero disabled, at least one component
is enabled, and each exact profile SHALL sum to `1.000000000`; co-change requires
`alpha + beta = 1.000000000`. Validation occurs before `Q` and invalid profiles
fail rather than being repaired.

### Requirement: Deterministic hotspot evidence
Hotspot components SHALL be canonicalized and combined with effective weights:

```text
C_f=Q(normalized(commit_count)); H_f=Q(normalized_log(churn))
T_f=Q(normalized(canonical_TaskKey_spread)); A_f=Q(normalized(author_spread))
R_f=Q(normalized(exact_epoch_second_span))
HotspotScore=Q(w_c*C_f+w_h*H_f+w_t*T_f+w_a*A_f+w_r*R_f)
```

Rankings SHALL be primary-category-local.

### Requirement: Canonical base co-change graph
`G0` SHALL contain retained logical files and exactly unordered file pairs with
`CommitCoChange>0`. Task co-change SHALL count canonical TaskKeys. Task evidence
MAY weight an existing edge but SHALL NOT create topology when commit co-change
is zero. Pair normalization/ranking, distinct-neighbor degree, incident evidence,
and `K_f` SHALL use `G0`.

### Requirement: Threshold graph and cluster aggregation
A configured threshold SHALL apply only to canonical `CombinedCoChange` with
inclusive `>=`:

```text
Gtheta = (V, {e in E0 : CombinedCoChange(e) >= theta})
```

`Gtheta` SHALL affect only clusters and cluster-derived candidates. It SHALL NOT
alter `G0`, pair normalization/ranking, `D_f`, `K_f`, or file scores.
`ClusterMaximum` and `ClusterAggregate` SHALL use qualifying `Gtheta` edges only.

### Requirement: Independent TaskKey evidence and temporal proximity
A multi-reference commit may contribute ordinary canonical TaskKey breadth/co-
change but SHALL not establish independent work alone. Independent pairs require
pair-exclusive canonical file-evidence commits on both sides.

Pair intervals SHALL be closed intervals over exact canonical committer epoch-
second integers:

```text
gap_seconds = later.start_epoch_second - earlier.end_epoch_second
days_between = 0                         when gap_seconds <= 0
               ceil(gap_seconds / 86400) when gap_seconds > 0
TemporalProximity = Q(1/(1+days_between))
```

Calendar dates, timezone, local midnight, host date ranges, and DST SHALL NOT
participate.

### Requirement: Cohort-safe centrality and OCP evidence
File centrality SHALL use raw `G0` incident commit/task degrees normalized in the
file's primary-category cohort and combined with effective co-change
`alpha/beta`; endpoint-cohort-normalized edge scores SHALL NOT be summed.

Repeated OCP editing SHALL union pair-exclusive commits per canonical TaskKey
across independent partners, deduplicate by SHA, then count repeats. Role hints
SHALL use the fixed ASCII tokenizer and exact token equality only.

### Requirement: Stable cohort-local ranking and candidates
File, `G0` pair, cluster, and candidate results SHALL remain grouped by comparable
cohort and rank only within that cohort. Candidate records SHALL carry source
evidence, thresholds, cohort identity, and caveats.

### Requirement: Successful reports and fail-closed diagnostics
Canonical Markdown/JSON SHALL be emitted only after all fail-closed validation
succeeds. Invalid refs, commit metadata/UTF-8/config, TaskKey overlap ambiguity,
missing required objects, or other canonical failures SHALL emit no partial
successful report, ranking, or candidate set. Failure diagnostics are a separate
command/error surface and SHALL NOT be successful report records.

### Requirement: Canonical JSON string escaping and bytes
Canonical strings SHALL contain valid Unicode scalar values and SHALL NOT be
Unicode-normalized during serialization. JSON escaping SHALL use `\"` for quote,
`\\` for backslash, standard short escapes for backspace/tab/newline/formfeed/
carriage-return, uppercase `\u00XX` for other U+0000..U+001F controls, literal
`/`, and direct UTF-8 for every other scalar.

Object properties SHALL follow #243's versioned schema order; dynamic map keys
SHALL use canonical scalar-value string ordering. Canonical bytes SHALL use UTF-8
without BOM, LF, two-space indentation, no trailing whitespace, exactly one
terminal LF, fixed nine-decimal non-exponent canonical reals, and the escaping
profile above. Report identity SHALL be over those exact bytes.

Successful reports SHALL expose canonical committer epoch integers/timezone-token
evidence, raw commit-metadata/task provenance, canonical TaskKeys/source evidence,
and accepted/`ambiguous_dag` rename evidence.

### Requirement: Contributor reference
The internal contributor reference SHALL remain synchronized with the capability,
including exact author/committer metadata parsing, TaskKey identity, reachability/
merge semantics, strict UTF-8/scalar ordering, formal DAG-safe exact rename and
lifecycle guard, raw-byte LCS line churn, cohort/numeric rules, `G0/Gtheta`,
temporal/OCP semantics, report/failure boundary, portable role tokens, canonical
JSON bytes, and interpretation limits. Public MkDocs navigation SHALL not
advertise the feature before it ships.
