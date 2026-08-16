## Context

Issue #235 defines Release Architecture Forensics theory before implementation.
Downstream #236–#244 need one reviewed source of truth not only for formulas but
also for Git evidence ingestion, commit metadata, task identity, path identity,
rename lineage, churn, graph topology, temporal evidence, and canonical report
bytes.

Repeated review showed that leaving any of those to a Git library, diff backend,
locale, Unicode runtime, JSON serializer, task-extractor iteration order, or
commit traversal order can make two otherwise conforming implementations
disagree. This design therefore intentionally specifies the first deterministic
profile down to those boundaries.

## Goals / Non-Goals

**Goals:**

- deterministic explicit-range Git evidence;
- raw commit-metadata semantics for authors/task messages;
- canonical namespaced task identity;
- one merge/root/file-event model;
- strict canonical path text and ordering;
- exact backend-independent and DAG-safe rename identity;
- deterministic line churn and binary/unavailable semantics;
- comparable cohort-local normalization;
- exact numeric and weight semantics;
- explicit `G0`/`Gtheta` separation and cluster aggregation;
- false-positive-controlled independent-task evidence;
- epoch-second temporal proximity;
- cohort-safe centrality and bounded OCP heuristics;
- byte-canonical JSON;
- optional .NET enrichment strictly downstream.

**Non-goals:**

- implementation in this PR;
- legacy-encoding transcoding in canonical v1 evidence;
- free-form task identifiers without a canonical namespace/decimal key;
- similarity rename heuristics in v1;
- resolving rename forks by timestamp or traversal order;
- merge-resolution-only file scoring in v1;
- byte-count churn fallback;
- locale/non-UTF8 path fallback;
- formal design-law proof;
- LLM-dependent canonical decisions.

## Decisions

### Main capability is the contract

`openspec/specs/release-architecture-forensics/spec.md` is normative.
`docs/internal/release-forensics.md` is the readable contributor reference. The
archived delta/design preserve why the rules were chosen.

### Raw commit objects are the metadata source

Author and task-reference evidence is parsed from raw commit object bytes, not
from a Git/log presentation API that may apply commit `encoding`, locale, code
page, Unicode normalization, or replacement behavior.

The raw `author` header is structurally parsed. Canonical author identity selects
non-empty email, else name, requires strict UTF-8, trims ASCII SP/HT, and maps only
ASCII `A-Z` to `a-z`. No Unicode normalization/case folding participates.

The raw commit-message payload also requires strict UTF-8 before task extraction.
The optional `encoding` header is retained as evidence but does not trigger
canonical transcoding.

Alternative: trust libgit/Git presentation strings. Rejected because two backends
can decode legacy metadata differently, changing author/task spread and every
score that consumes those metrics.

### Task identity is namespaced decimal, not source spelling

Each #237 extractor maps a match to:

```text
TaskKey = (ASCII-lowercase namespace, positive decimal id)
```

The ID is arbitrary precision and canonicalized without leading zeroes. Source
spellings/extractor IDs/spans remain evidence only. Same TaskKey matches dedupe;
the same message span mapping to different TaskKeys fails closed.

Alternative: use matched strings such as `#001` and `#1` as identity. Rejected
because format differences manufacture extra tasks and can create false
TaskCoChange/independence/OCP pressure. Alternative: extractor-order wins on
collisions. Rejected because configuration enumeration order would become a
hidden input.

### Reachability and merge policy

```text
Commits(from,to) = Reachable(to) \ Reachable(from)
```

One-parent commits use parent-tree -> commit-tree deltas, roots use the empty
tree, and merge commits are metadata-only for v1 file evidence.

Alternative: first-parent/per-parent/combined merge diff. Rejected because those
choices generate different file touches/churn/co-change and may double count
branch work already included by reachability. The cost -- possible undercount of
merge-resolution-only edits -- is explicit in reports.

### Strict UTF-8 paths and scalar-value ordering

Git paths are bytes. V1 requires strict UTF-8 and fails closed on invalid bytes.
There is no locale/code-page/replacement fallback.

Decoded strings preserve their exact Unicode scalar sequence. NFC/NFD/NFKC/NFKD
normalization is forbidden. Canonical ordinal ordering is lexicographic by
Unicode scalar numeric value, prefix-shorter-first.

Alternative: host-language ordinal/string comparer. Rejected because UTF-16
code-unit order, locale collation, filesystem normalization, and Unicode
normalization can disagree for non-BMP or canonically equivalent spellings.

### Exact rename starts local but must survive DAG validation

Inside one non-merge commit, a local exact-rename candidate is a one-to-one
delete/add relation with identical blob ID and no competing source/destination.
Similarity, copy inference, rename-with-edit, and ambient Git rename settings are
excluded.

Local candidates are not automatically one logical file. A connected/competing
candidate component canonicalizes only when exactly one sequence contains all
candidates, candidate commits are strictly increasing by Git ancestry, and each
destination is the next source.

An ancestry-incomparable fork/join or otherwise non-unique component is
`ambiguous_dag`; none of its candidates collapse identity. Timestamp/SHA order is
not allowed to manufacture a lineage from incomparable branches. Ambiguous
candidate delete/add entries remain ordinary file events.

Alternative: sort all rename candidates by canonical commit time and greedily
chain them. Rejected because timestamps are not topology and parallel branches
could be collapsed differently or incorrectly. Alternative: choose one branch.
Rejected because that silently discards equally valid evidence.

### Accepted exact rename is one touch and zero content churn

Only a local candidate accepted by the DAG-lineage rule collapses to one logical-
file `rename` event with zero canonical additions/deletions/churn. This retains
change pressure through `commit_count` without turning a pure move into backend-
dependent content churn.

A local candidate in `ambiguous_dag` gets no special zero-churn treatment because
its source/destination remain separate logical identities.

### Canonical text churn is raw-byte LCS, not Git diff output

For ordinary blob events, an absent add/delete side is the empty byte sequence.
Missing required repository objects fail closed.

Gitlinks/non-blob/non-line events and any event where a non-empty participating
blob contains NUL (`0x00`) use zero line counts plus
`binary_or_unavailable`. Byte counts, textconv, external diff, estimates, and
backend sentinels are forbidden.

Otherwise text line sequences are derived directly from raw blob bytes by LF
(`0x0A`) splitting; CR stays payload, terminal LF creates no extra line, and line
equality is exact byte equality. Let `L` be mathematical LCS length:

```text
deletions = old_line_count - L
additions = new_line_count - L
```

Alternative: consume `git diff --numstat` or a library's diff script. Rejected
because diff algorithms and tie-breaking can choose different edit scripts even
for identical blobs. LCS length uniquely fixes totals.

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
quantized before decisions and rendering. Profiles are exact validated ordinary
decimals summing to `1.000000000`; missing evidence never changes weights.

### `G0` is the stable evidence/scoring graph

```text
E0 = { unordered(a,b) : CommitCoChange(a,b) > 0 }
```

Task co-change counts canonical TaskKeys. It may weight an existing edge but
cannot create topology. Pair normalization/ranking, degree, incident evidence,
and `K_f` consume `G0`.

### `Gtheta` is cluster-only

```text
Gtheta = (V, { e in E0 : CombinedCoChange(e) >= theta })
```

Changing `theta` cannot feed back into file scoring or centrality.
ClusterMaximum and ClusterAggregate use qualifying `Gtheta` edges only.

### Independent-task evidence requires pair-exclusive TaskKey commits

A multi-reference commit can contribute ordinary canonical TaskKey breadth but
cannot prove parallel work. Pair intervals use pair-exclusive canonical file-
evidence commits. Repeated OCP evidence unions per-TaskKey pair-exclusive commits
and deduplicates by SHA before counting.

### Temporal proximity is epoch-second based

Closed pair intervals use committer epoch seconds:

```text
gap_seconds = later.start - earlier.end
days_between = 0                         if gap_seconds <= 0
               ceil(gap_seconds / 86400) if gap_seconds > 0
TemporalProximity = Q(1/(1+days_between))
```

Calendar dates, local midnight, timezone, and DST are explicitly irrelevant.

### Centrality does not sum incomparable edge scores

`K_f` uses raw `G0` incident commit/task degree, each normalized in the file's
primary-category cohort, then combines them using effective co-change
`alpha/beta`. This avoids summing edge scores normalized in different endpoint
cohorts.

### Role-name evidence is bounded and portable

V1 uses a fixed ASCII identifier tokenizer and exact token equality. No substring,
regex, culture, or Unicode case-folding behavior participates.

### Canonical JSON is a byte profile

Canonical JSON fixes:

- versioned property order;
- scalar-value ordering for dynamic keys;
- no Unicode normalization;
- exact string escaping (short standard controls, uppercase `\u00XX` for other
  C0 controls, literal `/`, direct UTF-8 for other scalars);
- UTF-8 without BOM;
- LF;
- two-space indentation;
- no trailing whitespace;
- one terminal LF;
- nine-decimal non-exponent canonical reals.

Reports additionally expose raw commit-metadata/task extraction status, canonical
TaskKeys/source evidence, and accepted versus `ambiguous_dag` rename evidence.
Artifact identity is over exact canonical JSON bytes.

## Risks / Trade-offs

- Legacy non-UTF8 selected author/message metadata fails closed rather than being
  transcoded.
- TaskKey v1 deliberately models one configured namespace plus positive decimal ID
  rather than arbitrary opaque strings.
- Merge-resolution-only edits may be understated.
- Rename-with-edit is intentionally missed in v1.
- Parallel/incomparable exact rename candidates can become `ambiguous_dag` false
  negatives rather than being guessed into a lineage.
- Accepted pure exact renames record touch pressure but zero content churn.
- Ambiguous rename candidates remain ordinary add/delete events and can therefore
  contribute line churn on their separate identities.
- NUL/non-line events have zero line churn with explicit limitation status.
- Raw-byte LCS can be more expensive than accepting a backend's numstat, but it
  fixes the metric mathematically and can be optimized without changing results.
- Strict UTF-8 rejects repositories with arbitrary non-UTF8 path bytes.
- Scalar-preserving path identity means NFC/NFD spellings remain distinct.
- Category-local scores cannot be globally compared.
- Task refs/authors remain imperfect proxies even after deterministic identity.

## Verification expectations

The downstream suite must cover at least:

- raw commit author/message extraction independent of Git presentation APIs;
- invalid selected author/message UTF-8 failure and ASCII-only author casing;
- TaskKey `#001/#1` normalization, namespace separation, and extractor collision;
- reachability/root/merge fixtures;
- invalid UTF-8 path failure;
- precomposed/decomposed path distinction and non-BMP scalar ordering;
- linear exact rename, same-commit split, alias cycle, and parallel DAG fork;
- accepted exact rename one-touch/zero-churn behavior;
- `ambiguous_dag` candidate ordinary add/delete behavior;
- raw-LF/LCS line counts with ambiguous diff scripts;
- NUL/non-line zero-churn status and missing-object failure;
- distinct-commit file count;
- category isolation and numeric golden vectors;
- exact weight validation;
- task-only no-`G0` edge;
- threshold invariance of file scoring;
- qualifying-edge cluster aggregate;
- multi-ref false-positive controls and per-TaskKey SHA-union repeated edits;
- 25-hour temporal gap => two days;
- mixed endpoint-cohort centrality;
- ASCII role-token vectors;
- JSON scalar key ordering/escaping/byte identity.

## Migration Plan

1. #236 implements raw commit metadata, canonical TaskKey extraction mechanics,
   reachability, strict path model, DAG-safe exact rename, canonical file events/
   raw-byte LCS churn, and CLI family.
2. #237 implements schema-backed TaskKey extractor namespaces/patterns and other
   configurable values without semantic override.
3. #238/#239 implement hotspot and `G0/Gtheta` evidence using canonical authors/
   TaskKeys.
4. #240/#241 implement independent-TaskKey bottleneck/OCP evidence.
5. #242 optionally enriches completed Git findings.
6. #243 renders the versioned canonical report.
7. #244 dogfoods and locks conformance/governance.

## Open Questions

None for the first deterministic profile. Any change to commit metadata decoding,
TaskKey identity, commit-set/merge handling, path encoding/order/normalization,
rename-lineage ambiguity, line-churn semantics, cohort/graph topology, weights/
numeric scale, temporal formula, role tokenizer, or canonical JSON requires
reviewed specification work with compatibility/migration notes.
