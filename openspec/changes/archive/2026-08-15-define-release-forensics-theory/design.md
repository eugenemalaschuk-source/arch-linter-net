## Context

Issue #235 defines Release Architecture Forensics theory before implementation.
Downstream #236–#244 need one reviewed source of truth not only for formulas but
also for Git evidence ingestion, raw metadata/time parsing, task identity, path
identity, rename lineage/lifecycle, churn, graph topology, temporal evidence, and
canonical report bytes.

Repeated review showed that leaving any of those to a Git library, diff backend,
locale, Unicode runtime, host date/time API, JSON serializer, task-extractor
iteration order, or commit traversal order can make conforming implementations
disagree. This design intentionally specifies the first deterministic profile down
to those boundaries.

## Goals / Non-Goals

**Goals:**

- deterministic explicit-range Git evidence;
- raw author/message/committer metadata semantics;
- exact integer temporal evidence independent of host calendar APIs;
- canonical namespaced task identity;
- one merge/root/file-event model;
- strict canonical path text and ordering;
- exact backend-independent and DAG/lifecycle-safe rename identity;
- deterministic line churn and binary/unavailable semantics;
- comparable cohort-local normalization;
- exact numeric and weight semantics;
- explicit `G0`/`Gtheta` separation and cluster aggregation;
- false-positive-controlled independent-task evidence;
- cohort-safe centrality and bounded OCP heuristics;
- explicit successful-report vs failure-diagnostic boundary;
- byte-canonical JSON;
- optional .NET enrichment strictly downstream.

**Non-goals:**

- implementation in this PR;
- legacy-encoding transcoding in canonical v1 evidence;
- calendar conversion of canonical committer timestamps;
- free-form task identifiers without canonical namespace/decimal key;
- similarity rename heuristics in v1;
- resolving rename forks by timestamp/traversal order;
- chaining identity across path deletion/recreation;
- merge-resolution-only file scoring in v1;
- byte-count churn fallback;
- locale/non-UTF8 path fallback;
- partial canonical reports after validation failure;
- formal design-law proof;
- LLM-dependent canonical decisions.

## Decisions

### Main capability is the contract

`openspec/specs/release-architecture-forensics/spec.md` is normative.
`docs/internal/release-forensics.md` is the readable contributor reference. The
archived delta/design preserve why the rules were chosen.

### Raw commit objects are the metadata source

Author, committer, and task-reference evidence is parsed from raw commit object
bytes, not Git/log presentation APIs that may apply commit `encoding`, locale,
code page, Unicode normalization/replacement, or wall-clock conversion.

Exactly one `author` and one `committer` header are required. Both use the same
right-to-left suffix shape:

```text
<identity-bytes> SP <timestamp-token> SP <timezone-token>
```

with timestamp `-?[0-9]+` and timezone `[+-][0-9]{4}`. Author identity further
requires a final angle-bracketed email split by the last `<` and final `>`.
Canonical author selects non-empty email else name, strict-UTF8 decodes the
selected bytes, trims ASCII SP/HT, maps ASCII `A-Z` to `a-z`, and performs no
Unicode normalization/full case folding.

Alternative: trust libgit/Git presentation strings. Rejected because backends can
decode legacy metadata differently, changing author/task spread.

### Committer time is an exact integer, not a date

The committer timestamp token is interpreted as arbitrary-precision signed Unix
epoch seconds. The timezone token is validated and retained only as metadata; it
does not adjust the epoch value. Commit ordering and all temporal spans/gaps use
exact integer arithmetic; full SHA breaks equal-time ties.

Alternative: parse into `DateTime`, apply timezone, or use floating Unix seconds.
Rejected because host calendar ranges, timezone conversion, DST, overflow, and
floating precision can make identical Git objects produce different results.

### Task identity is namespaced decimal, not source spelling

Each #237 extractor maps a match to:

```text
TaskKey = (ASCII-lowercase namespace, positive decimal id)
```

The ID is arbitrary precision and canonicalized without leading zeroes. `#0` is
not a default key. Every match carries a non-empty half-open byte span in the raw
message payload. Same TaskKey matches dedupe; overlapping spans producing
different TaskKeys fail closed. Non-overlapping references may remain distinct.

Alternative: use matched strings (`#001` vs `#1`) as identity. Rejected because
format differences manufacture tasks and false coupling/independence. Alternative:
extractor-order wins. Rejected because config enumeration becomes a hidden input.

### Reachability and merge policy

```text
Commits(from,to) = Reachable(to) \ Reachable(from)
```

One-parent commits use parent-tree -> commit-tree deltas, roots use the empty
tree, and merge commits are metadata-only for v1 file evidence.

Alternative: first-parent/per-parent/combined merge diff. Rejected because those
choices generate different file touches/churn/co-change and can double count work
already represented by reachable non-merge commits. Merge-resolution-only edits
may be understated and that limitation is reported.

### Strict UTF-8 paths and scalar-value ordering

Git paths are bytes. V1 requires strict UTF-8 and fails closed on invalid bytes.
There is no locale/code-page/replacement fallback.

Decoded strings preserve exact Unicode scalar sequence. NFC/NFD/NFKC/NFKD
normalization is forbidden. Canonical ordinal ordering is lexicographic Unicode
scalar numeric value, prefix-shorter-first.

Alternative: host-language ordinal/string comparer. Rejected because UTF-16 code-
unit order, locale collation, filesystem normalization, and Unicode normalization
can disagree for non-BMP or canonically equivalent spellings.

### Rename candidates use an explicit overlap graph

Inside one non-merge commit, a local exact-rename candidate is a one-to-one
delete/add relation with identical blob ID and no competing source/destination.
Similarity, copy inference, rename-with-edit, and ambient Git rename settings are
excluded.

For every candidate `c`, define endpoint set `{src(c),dst(c)}`. The undirected
candidate-overlap graph connects two candidates iff their endpoint sets intersect.
Connected components, rather than enumeration order, define possible lineages.

A component canonicalizes only if exactly one permutation contains every
candidate and:

1. all candidate commits are strictly ordered by Git ancestry;
2. every destination equals the next source;
3. the shared path has no ordinary add/delete event in a non-merge commit strictly
   between adjacent candidate commits.

Rule 3 prevents path-name reuse after delete/recreate from being mistaken for file
continuity. A fork/join, non-unique sequence, or lifecycle break becomes
`ambiguous_dag`; none of those candidates collapse identity. Timestamp/SHA order
cannot repair topology or lifecycle ambiguity.

Alternative: sort candidates by time and greedily chain. Rejected because time is
not ancestry and path spelling is not lifetime identity. Alternative: choose one
branch. Rejected because that discards equally valid evidence.

### Accepted exact rename is one touch and zero content churn

Only a candidate accepted by the formal lineage rule collapses to one `rename`
event with zero canonical additions/deletions/churn. `commit_count` still records
touch pressure. A candidate in `ambiguous_dag` remains ordinary delete/add events.

### Canonical text churn is raw-byte LCS, not Git diff output

For ordinary blob events, an absent add/delete side is empty bytes. Missing
required objects fail closed. Gitlinks/non-blob/non-line events and any event with
NUL in a non-empty participating blob use zero line counts plus
`binary_or_unavailable`; bytes/textconv/external diff/estimates are forbidden.

Otherwise line sequences come directly from raw blob bytes split on LF; CR remains
payload, terminal LF adds no line, equality is exact bytes. Let `L` be mathematical
LCS length:

```text
deletions = old_line_count - L
additions = new_line_count - L
```

Alternative: use `git diff --numstat` or a library diff script. Rejected because
diff algorithms/tie-breaking can choose different edit scripts. LCS length fixes
totals mathematically.

`commit_count(f)` counts distinct canonical file-evidence commits, not raw delta
entries.

### Cohort-local normalization

#237 ignores remove files before score populations and `G0`. File components
normalize inside primary-category cohorts; edge components inside unordered
endpoint-category cohorts. Presentation suppression cannot rescore evidence.
Cross-cohort normalized values are not globally comparable.

### Nine-decimal canonical numeric model

```text
Q(v) = round-half-to-even(v, 9 decimal places)
```

Canonical components, proximity, edge weights, scores, and thresholds are
quantized before decisions/rendering. Profiles are exact validated ordinary
decimals summing to `1.000000000`; missing evidence never changes weights.

### `G0` is the stable evidence/scoring graph

```text
E0 = { unordered(a,b) : CommitCoChange(a,b) > 0 }
```

Task co-change counts canonical TaskKeys. It may weight an existing edge but cannot
create topology. Pair normalization/ranking, degree, incident evidence, and `K_f`
consume `G0`.

### `Gtheta` is cluster-only

```text
Gtheta = (V, { e in E0 : CombinedCoChange(e) >= theta })
```

Changing `theta` cannot feed back into file scoring or centrality.
ClusterMaximum and ClusterAggregate use qualifying `Gtheta` edges only.

### Independent-task evidence requires pair-exclusive TaskKey commits

A multi-reference commit can contribute ordinary TaskKey breadth but cannot prove
parallel work. Pair intervals use pair-exclusive canonical file-evidence commits.
Repeated OCP evidence unions per-TaskKey pair-exclusive commits and deduplicates by
SHA before counting.

### Temporal proximity is exact epoch-second arithmetic

Closed pair intervals use exact committer epoch integers:

```text
gap_seconds = later.start - earlier.end
days_between = 0                         if gap_seconds <= 0
               ceil(gap_seconds / 86400) if gap_seconds > 0
TemporalProximity = Q(1/(1+days_between))
```

Calendar dates, local midnight, timezone, host date range, floating seconds, and
DST are irrelevant.

### Centrality does not sum incomparable edge scores

`K_f` uses raw `G0` incident commit/task degree, each normalized in the file's
primary-category cohort, then combines them using effective co-change
`alpha/beta`. This avoids summing edge scores normalized in different endpoint
cohorts.

### Role-name evidence is bounded and portable

V1 uses a fixed ASCII identifier tokenizer and exact token equality. No substring,
regex, culture, or Unicode case-folding behavior participates.

### Successful reports are atomic with respect to validation

A canonical Markdown/JSON report exists only after all fail-closed validation
passes. Invalid refs/metadata/UTF-8/config, TaskKey overlap ambiguity, or missing
required Git objects produce no partial ranking or candidate set. Diagnostics are
a separate error surface and are not records in successful canonical report bytes.

Alternative: serialize partial evidence plus errors. Rejected because report
identity would then depend on how far a backend progressed before the same failure.

### Canonical JSON is a byte profile

Canonical JSON fixes versioned property order, scalar-value dynamic-key order, no
Unicode normalization, exact string escaping, UTF-8 without BOM, LF, two-space
indentation, no trailing whitespace, one terminal LF, and nine-decimal non-
exponent canonical reals.

Successful reports expose exact committer epoch integers/timezone evidence, raw
metadata/task provenance, canonical TaskKeys/source evidence, and accepted versus
`ambiguous_dag` rename evidence. Artifact identity is over exact bytes.

## Risks / Trade-offs

- Legacy non-UTF8 selected author/message metadata fails closed rather than being
  transcoded.
- V1 TaskKeys deliberately model configured namespace + positive decimal rather
  than arbitrary opaque strings.
- Exact arbitrary-precision temporal integers can require `BigInteger`-style
  implementation rather than host date/time types.
- Merge-resolution-only edits may be understated.
- Rename-with-edit is intentionally missed.
- Parallel/incomparable/lifecycle-broken rename candidates become conservative
  `ambiguous_dag` false negatives rather than guessed lineages.
- Accepted pure exact renames record touch pressure but zero content churn.
- Ambiguous rename candidates remain ordinary add/delete events and may contribute
  churn on separate identities.
- NUL/non-line events have zero line churn with explicit limitation status.
- Raw-byte LCS may be more expensive than backend numstat, but can be optimized
  without changing results.
- Strict UTF-8 rejects repositories with arbitrary non-UTF8 path bytes.
- Scalar-preserving path identity means NFC/NFD spellings remain distinct.
- Category-local scores cannot be globally compared.
- Task refs/authors remain imperfect proxies even after deterministic identity.

## Verification expectations

The downstream suite must cover at least:

- exact author/committer raw header grammar and malformed/duplicate failures;
- timezone-token invariance and large epoch values outside host calendar range;
- invalid selected author/message UTF-8 and ASCII-only author casing;
- TaskKey `#001/#1`, `#0`, namespace separation, overlapping extractor collision;
- reachability/root/merge fixtures;
- invalid UTF-8 path failure;
- precomposed/decomposed path distinction and non-BMP scalar ordering;
- explicit candidate-overlap components, linear rename, same-commit split, alias
  cycle, parallel DAG fork, delete/recreate lifecycle break;
- accepted exact rename one-touch/zero-churn;
- `ambiguous_dag` ordinary add/delete behavior;
- raw-LF/LCS counts with ambiguous diff scripts;
- NUL/non-line zero-churn and missing-object failure;
- distinct-commit file count;
- category isolation and numeric vectors;
- exact weight validation;
- task-only no-`G0` edge;
- threshold invariance of file scoring;
- qualifying-edge cluster aggregate;
- multi-ref false-positive controls and per-TaskKey SHA-union repeated edits;
- 25-hour temporal gap => two days;
- mixed endpoint-cohort centrality;
- ASCII role-token vectors;
- failed analysis emits no successful report;
- JSON scalar ordering/escaping/byte identity.

## Migration Plan

1. #236 implements raw author/committer/message parsing, exact temporal integers,
   canonical TaskKey extraction, reachability, strict paths, candidate-overlap
   graph/DAG-lifecycle-safe rename, canonical file events/raw-byte LCS churn, and
   CLI diagnostics.
2. #237 implements schema-backed TaskKey extractor namespaces/patterns and other
   configurable values without semantic override.
3. #238/#239 implement hotspot and `G0/Gtheta` evidence using canonical authors/
   TaskKeys/files.
4. #240/#241 implement independent-TaskKey bottleneck/OCP evidence with exact
   temporal integers.
5. #242 optionally enriches completed Git findings.
6. #243 renders only successful versioned canonical reports and keeps diagnostics
   separate.
7. #244 dogfoods and locks conformance/governance.

## Open Questions

None for the first deterministic profile. Any change to commit metadata/time
parsing, TaskKey identity, commit-set/merge handling, path encoding/order/
normalization, rename component/lifecycle semantics, line churn, cohort/graph
topology, weights/numeric scale, temporal formula, report/failure boundary, role
tokenizer, or canonical JSON requires reviewed specification work with
compatibility/migration notes.
