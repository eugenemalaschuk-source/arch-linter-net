## Context

Issue #235 defines Release Architecture Forensics theory before implementation.
Downstream #236–#244 need one reviewed source of truth not only for formulas but
also for ref/object resolution, raw Git metadata, exact temporal integers, task
identity/provenance, path identity, rename lineage/lifecycle, churn, graph
semantics, failure behavior, and canonical report bytes.

Repeated review showed that leaving any of those to Git DWIM, a Git library, diff
backend, locale, Unicode runtime, host date/time API, JSON serializer, extractor
iteration order, or commit traversal order can make otherwise conforming
implementations disagree. This design intentionally fixes one conservative v1
profile down to those boundaries.

## Goals / Non-Goals

**Goals:**

- deterministic authored-ref resolution and full Git object-ID identity;
- deterministic explicit-range Git evidence;
- raw author/message/committer metadata semantics;
- exact integer temporal evidence independent of host calendar APIs;
- canonical namespaced task identity plus mandatory source provenance;
- one merge/root/file-event model;
- strict canonical path text/order and an explicit same-path baseline identity;
- exact backend-independent and DAG/lifecycle-safe rename identity with mandatory
  candidate provenance;
- deterministic line churn and binary/unavailable semantics;
- comparable cohort-local normalization;
- exact numeric and weight semantics;
- explicit `G0`/`Gtheta` separation and cluster aggregation;
- false-positive-controlled independent-task evidence;
- cohort-safe centrality and bounded OCP heuristics;
- explicit successful-report vs failure-diagnostic boundary;
- mandatory canonical provenance and byte-canonical JSON;
- optional .NET enrichment strictly downstream.

**Non-goals:**

- implementation in this PR;
- Git revision-expression/DWIM semantics in v1 operands;
- abbreviated object IDs;
- legacy-encoding transcoding in canonical v1 evidence;
- calendar conversion of canonical committer timestamps;
- arbitrary opaque task identifiers;
- similarity rename heuristics;
- resolving rename forks by timestamp/traversal order;
- path-lifetime segmentation across same-path delete/recreate;
- chaining rename identity across path delete/recreate lifecycle breaks;
- merge-resolution-only file scoring;
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

### Git object IDs and ref operands are deliberately smaller than Git revision syntax

Canonical Git object IDs use the repository-declared hash format and full lowercase
hex digests. Authored operands resolve only as literal `HEAD`, full-length object
ID, exact fully-qualified `refs/...`, or shorthand searched exactly under
`refs/tags/` and `refs/heads/` with collision failure. Symbolic refs are followed
with cycle detection and annotated tags are peeled recursively to a final commit.

Alternative: `rev-parse`/DWIM/revision-expression semantics. Rejected because
branch/tag precedence, abbreviated IDs, reflogs, ancestry operators, and backend
revision parsers create hidden canonical inputs before the already-deterministic
reachability formula runs.

### Raw commit objects are the metadata source

Author, committer, and task-reference evidence is parsed from raw commit-object
bytes, not Git/log presentation APIs that may apply commit `encoding`, locale,
code page, Unicode replacement, or wall-clock conversion.

Exactly one `author` and one `committer` header are required. Both use the same
right-to-left suffix shape:

```text
<identity-bytes> SP <timestamp-token> SP <timezone-token>
```

with timestamp `-?[0-9]+` and timezone `[+-][0-9]{4}`. Author identity further
requires a final angle-bracketed email split by the last `<` and final `>`.
Canonical author selects non-empty email else name, strict-UTF8 decodes selected
bytes, trims ASCII SP/HT, maps ASCII `A-Z` to `a-z`, and performs no Unicode
normalization/full case folding.

Every direct `encoding ` header is retained as raw-value lowercase-hex canonical
provenance, but never changes decoding.

Alternative: trust Git/libgit presentation strings. Rejected because backends can
decode legacy metadata differently, changing author/task spread and canonical
report bytes.

### Committer time is an exact integer, not a date

The committer timestamp token is arbitrary-precision signed Unix epoch seconds.
The timezone token is validated/retained but does not adjust the epoch. Commit
ordering and all temporal spans/gaps use exact integer arithmetic; full canonical
commit ID breaks equal-time ties.

Alternative: `DateTime`, timezone adjustment, or floating Unix seconds. Rejected
because host calendar ranges, DST, overflow, and floating precision can vary.

### Task identity is namespaced decimal with mandatory provenance

Each #237 extractor has a stable extractor ID and maps a match to:

```text
TaskKey = (ASCII-lowercase namespace, positive decimal id)
```

The ID is arbitrary precision and canonicalized without leading zeroes. Every
match retains extractor ID, TaskKey, raw half-open message byte span, and matched
UTF-8 substring. Same-key evidence can dedupe while the TaskKey set dedupes
independently. Overlapping spans producing different TaskKeys fail closed.

The default `issue` extractor matches `#<positive decimal>` only when the adjacent
scalars are outside `[A-Za-z0-9_#]`; therefore `abc#12`, `#12foo`, `##12`,
`#12#13`, and `#0` do not match.

Alternative: source spelling as identity. Rejected because `#001` vs `#1`
manufactures task spread. Alternative: extractor-order wins. Rejected because
configuration enumeration becomes a hidden input. Alternative: leave lexical
boundaries to each backend. Rejected because even the default profile would then
produce different task evidence.

### Reachability and merge policy

```text
Commits(from,to) = Reachable(to) \ Reachable(from)
```

One-parent commits use parent-tree -> commit-tree deltas, roots use the empty
tree, and merge commits are metadata-only for v1 file evidence.

Alternative: first-parent/per-parent/combined merge diff. Rejected because those
choices generate different file touches/churn/co-change and can double count work
already represented by reachable non-merge commits.

### Strict UTF-8 paths and scalar-value ordering

Git paths are bytes. V1 requires strict UTF-8 and fails closed on invalid bytes.
Decoded strings preserve exact Unicode scalar sequence; normalization is forbidden.
Canonical ordering is lexicographic scalar numeric value, prefix-shorter-first.

Alternative: host-language ordinal/string comparer. Rejected because UTF-16
code-unit order, locale/filesystem collation, and normalization can disagree.

### Baseline identity is pathname-wide for v1

Before accepted rename unions, every canonical event with the same exact canonical
path belongs to one baseline path identity across the analyzed commit set, even
after deletion/recreation or unrelated blob replacement.

Alternative: split path lifetimes. Deferred because lifetime segmentation needs a
new stable logical-file ID/tie-break model and more topology semantics. V1 chooses
one explicit pathname identity and reports the known risk: unrelated generations
reusing one pathname can be over-aggregated.

### Rename candidates use an explicit overlap graph and lifecycle guard

Inside one non-merge commit, a local exact candidate is a one-to-one delete/add
with identical blob ID and no competing source/destination. Similarity/copy/
rename-with-edit and ambient Git settings are excluded.

Candidate endpoint-set intersection defines an undirected overlap graph; connected
components are potential lineages. A component canonicalizes only if exactly one
all-candidate permutation exists, every earlier candidate commit is a strict Git
ancestor of every later candidate, each destination equals the next source, and
no ordinary add/delete of the shared path occurs strictly between adjacent
candidate commits.

The last condition is a lifecycle guard. A fork/join, non-unique sequence, or
lifecycle break becomes `ambiguous_dag`; no cross-path union occurs. Timestamp/ID
order cannot repair topology.

Every local candidate/component is mandatory canonical provenance with explicit
accepted/ambiguous outcome and deterministic ordering.

Alternative: time-sort and greedily chain. Rejected because time is not ancestry.
Alternative: infer continuity from reused path names. Rejected because same-path
baseline identity does not prove cross-path rename continuity after delete/recreate.

### Accepted exact rename is one touch and zero content churn

Only an accepted candidate collapses to one `rename` event with zero canonical
additions/deletions/churn. A candidate in `ambiguous_dag` stays ordinary delete/add
events on separate baseline identities.

### Canonical text churn is raw-byte LCS, not Git diff output

Missing required objects fail closed. Gitlinks/non-blob/non-line events and NUL-
containing participating blobs use zero line counts plus
`binary_or_unavailable`. Otherwise lines come directly from raw bytes split on LF;
CR remains payload and equality is exact bytes. With mathematical LCS length `L`:

```text
deletions = old_line_count - L
additions = new_line_count - L
```

Alternative: Git numstat/library diff scripts. Rejected because diff algorithms
and tie-breaking can yield different scripts/totals.

### Cohort-local normalization and canonical numbers

#237 ignores remove files before score populations and `G0`. File components
normalize inside primary-category cohorts; edge components inside unordered
endpoint-category cohorts. Cross-cohort normalized values are not globally
comparable.

```text
Q(v) = round-half-to-even(v, 9 decimal places)
```

Canonical derived reals are quantized before decisions/rendering. Weight profiles
are exact validated decimals summing to `1.000000000`; missing evidence never
changes weights.

### `G0` is the stable scoring graph; `Gtheta` is cluster-only

`G0` topology exists iff `CommitCoChange>0`; TaskCoChange counts canonical
TaskKeys and may weight but not create an edge. Degree, incident evidence, and
`K_f` consume `G0`. `Gtheta` contains canonical combined edges `>= theta` and
only drives clusters/candidates; threshold changes cannot rescore files.

### Independent-task evidence and temporal proximity

Multi-reference commits do not establish independence. Pair-exclusive canonical
TaskKey commits define independent pairs and closed exact epoch-second intervals:

```text
gap_seconds = later.start - earlier.end
days_between = 0                         if gap_seconds <= 0
               ceil(gap_seconds / 86400) if gap_seconds > 0
TemporalProximity = Q(1/(1+days_between))
```

Repeated OCP evidence unions qualifying commits per TaskKey and deduplicates by
SHA. `K_f` uses raw incident commit/task degrees normalized in the file cohort.

### Successful reports are atomic with respect to validation

Canonical Markdown/JSON exists only after all fail-closed validation passes.
Invalid refs/metadata/UTF-8/config, TaskKey overlap ambiguity, or missing required
objects produce no partial ranking/candidate/report. Diagnostics are a separate
error surface.

Alternative: serialize partial evidence plus errors. Rejected because artifact
identity could depend on how far a backend progressed before the same failure.

### Canonical provenance is mandatory, not “best effort”

Successful canonical JSON always contains repository hash format, authored/
resolved operands, exact committer/timezone evidence, ordered `encoding ` header
raw provenance, canonical authors, full ordered TaskKey match provenance,
complete ordered rename-candidate/component provenance, and all scoring/report
evidence required by #243.

Extra debug evidence may exist only outside canonical JSON and cannot affect
canonical artifact identity.

### Canonical JSON is a byte profile

Canonical JSON fixes versioned property order, scalar-value dynamic-key order, no
Unicode normalization, exact escaping, UTF-8 without BOM, LF, two-space
indentation, no trailing whitespace, one terminal LF, full lowercase Git object
IDs, exact non-exponent raw integers, and nine-decimal non-exponent canonical
reals. Artifact identity is over exact bytes.

## Risks / Trade-offs

- Ref operands intentionally support less syntax than Git revision expressions.
- Legacy non-UTF8 selected author/message metadata fails closed rather than being
  transcoded.
- V1 TaskKeys deliberately model configured namespace + positive decimal rather
  than arbitrary opaque IDs.
- Exact arbitrary-precision temporal integers can require BigInteger-style
  implementation instead of host date/time types.
- Same-path delete/recreate events can over-aggregate unrelated generations.
- Merge-resolution-only edits may be understated.
- Rename-with-edit is intentionally missed.
- Parallel/incomparable/lifecycle-broken rename candidates become conservative
  `ambiguous_dag` false negatives rather than guessed lineages.
- Accepted exact renames record touch pressure but zero content churn.
- NUL/non-line events have zero line churn with explicit limitation status.
- Raw-byte LCS may be more expensive than backend numstat, but can be optimized
  without changing results.
- Strict UTF-8 rejects repositories with arbitrary non-UTF8 path bytes.
- Scalar-preserving path identity means NFC/NFD spellings remain distinct.
- Category-local scores cannot be globally compared.
- Task refs/authors remain imperfect proxies even after deterministic identity.

## Verification expectations

The downstream suite must cover at least:

- SHA-1/SHA-256 full object IDs, HEAD/exact refs/shorthand, branch/tag collision,
  annotated-tag peeling, unsupported revision expressions;
- exact author/committer raw grammar and malformed/duplicate failures;
- timezone-token invariance and large epoch values;
- invalid selected author/message UTF-8 and ASCII-only author casing;
- `encoding ` provenance ordering;
- TaskKey `#001/#1`, `#0`, namespace separation, default lexical boundaries,
  overlapping extractor collision, provenance ordering/dedup;
- reachability/root/merge fixtures;
- invalid UTF-8 path and Unicode scalar-order fixtures;
- plain same-path delete/readd baseline identity;
- candidate-overlap components, linear rename, split, alias cycle, parallel fork,
  delete/recreate lifecycle break, mandatory candidate provenance;
- accepted rename zero churn and ambiguous ordinary add/delete behavior;
- raw-LF/LCS counts, NUL/non-line status, missing-object failure;
- category isolation, numeric/weight vectors;
- task-only no-`G0` edge, threshold invariance, qualifying-edge cluster aggregate;
- multi-ref controls, per-TaskKey SHA-union repeated edits, 25-hour temporal gap,
  mixed endpoint centrality, ASCII role tokens;
- failed analysis emits no successful report;
- canonical JSON scalar/provenance ordering, escaping, and byte identity.

## Migration Plan

1. #236 implements deterministic ref/object resolution, raw metadata/time parsing,
   TaskKey/provenance extraction, reachability, strict paths, baseline same-path
   identity, candidate-overlap/DAG-lifecycle-safe rename, file events/LCS churn,
   and CLI diagnostics.
2. #237 implements schema-backed extractor IDs/namespaces/patterns and other
   configurable values without semantic override.
3. #238/#239 implement hotspot and `G0/Gtheta` evidence using canonical
   authors/TaskKeys/files.
4. #240/#241 implement independent-TaskKey bottleneck/OCP evidence with exact
   temporal integers.
5. #242 optionally enriches completed Git findings.
6. #243 renders only successful versioned canonical reports, mandatory provenance,
   and separate deterministic diagnostics.
7. #244 dogfoods and locks conformance/governance.

## Open Questions

None for the first deterministic profile. Any change to ref resolution/object-ID
identity, metadata/time parsing, TaskKey identity/boundaries/provenance, path
baseline identity, rename component/lifecycle semantics, line churn, cohort/graph
topology, numeric scale, report/failure boundary, mandatory provenance, role
tokenizer, or canonical JSON requires reviewed specification work with
compatibility/migration notes.
