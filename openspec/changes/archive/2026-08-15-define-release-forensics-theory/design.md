## Context

Issue #235 defines Release Architecture Forensics theory before implementation.
Downstream #236–#243 need one reviewed source of truth for Git-range identity,
file-touch evidence, rename recognition, churn, normalization, numeric precision,
task independence, graph semantics, ranking, and canonical report bytes.

The feature is planned, not shipped, so public MkDocs pages must not present it
as current product behavior.

Repeated code-review and merge-safety passes showed that deterministic formulas
alone were insufficient: canonical results also depend on how Git commits, file
deltas, renames, binary/unavailable line counts, task intervals, paths, and JSON
strings are interpreted. Those lower-level rules therefore belong in #235 rather
than being implementation choices in #236/#239/#243.

## Goals / Non-Goals

**Goals:**

- deterministic Git-only evidence and canonical scoring;
- one reachability/merge/file-event model;
- conservative backend-independent rename recognition;
- deterministic churn and commit-count semantics;
- explicit handling for binary/unavailable line counts;
- strict canonical Git-path text semantics;
- comparable normalization cohorts and exact numeric precision;
- validated effective profiles and threshold semantics;
- explicit `G0`/`Gtheta` ownership and cluster aggregation;
- false-positive control for multi-reference task evidence;
- exact epoch-second temporal proximity;
- cohort-safe graph centrality and ranking;
- portable ASCII role-token behavior;
- one canonical JSON string/byte profile;
- optional .NET enrichment strictly downstream.

**Non-Goals:**

- implementing CLI, policy schema, analyzer, reports, or Roslyn enrichment;
- a second config language or executable;
- similarity-based rename heuristics in v1;
- fully modeling merge-resolution-only edits in v1;
- byte-based churn fallback for binaries;
- locale fallback for non-UTF8 Git paths;
- formal proof of coupling, merge conflicts, or OCP violations.

## Decisions

### Internal reference plus capability spec

The main OpenSpec capability is the testable contract and
`docs/internal/release-forensics.md` is the readable contributor reference.
Public navigation remains unchanged until implementation ships.

### Canonical reachability range and merge-file policy

The canonical commit set is:

```text
Commits(from,to) = Reachable(to) \ Reachable(from)
```

Each reachability set includes its ref. Commit ordering is committer UTC epoch
second then ordinal full SHA. Ordinary one-parent commits use parent-tree →
commit-tree deltas; roots use the empty tree.

Merge commits remain range metadata but do not contribute file-derived evidence
in v1.

Alternative considered: first-parent, per-parent, or combined merge diffs.
Rejected because each creates different touch/churn/co-change evidence and can
double-count branch changes already present in the reachability range. The
trade-off—possible undercount of merge-resolution-only edits—is explicit output.

### Strict UTF-8 Git path model

Git tree paths are bytes. V1 requires strict UTF-8 for every path that enters
canonical evidence. Invalid UTF-8 fails closed before classification or report
serialization.

Alternative considered: locale/code-page fallback or replacement decoding.
Rejected because canonical paths would then depend on host environment or
library behavior. A future byte-preserving path representation would require a
reviewed compatibility change.

### Exact one-to-one blob rename recognition

A canonical rename exists only when one non-merge commit has a one-to-one
delete/add relation with the same Git blob object ID. Similarity and copy
inference do not participate in canonical identity in v1.

Alternative considered: Git-style similarity detection. Rejected because
thresholds, candidate limits, copy behavior, backend versions, attributes, and
ambient Git configuration would become hidden canonical inputs.

The last in-range path remains canonical. Distinct non-canonical historical paths
are aliases ordered by first canonical occurrence then ordinal path. Split,
copy, many-to-one, and rename-with-edit cases stay separate.

### Exact rename is a touch, not churn

After exact rename recognition, the matching delete/add raw entries collapse into
one canonical `rename` file event. Old/new paths and blob identity remain evidence,
but canonical additions, deletions, and churn are all zero.

Alternative considered: preserve raw delete/add line counts. Rejected because a
pure move could become approximately twice the file length in one backend while
another backend reports zero content change. Canonical history pressure should
count the commit touch but not invent content churn.

`commit_count(f)` counts distinct canonical file-evidence commits, never raw
delta-entry multiplicity.

### Binary/unavailable line counts use explicit zero semantics

A non-rename text event uses meaningful line additions/deletions when available.
A binary delta or any delta lacking meaningful line counts has additions `0`,
deletions `0`, and explicit `line_count_status = binary_or_unavailable`.

Alternative considered: byte counts, estimated lines, textconv, or backend
sentinels. Rejected because those are not the same metric as line churn and vary
by implementation/configuration. The zero is therefore accompanied by a visible
limitation marker rather than being silently interpreted as “no change”.

### Comparable normalization cohorts

#237 ignores remove files before base-graph/score population construction. File
metrics normalize within primary-category cohorts; base-edge metrics within
unordered endpoint-category cohorts. Presentation suppression cannot affect
scores. Cross-cohort normalized values are not one common absolute scale.

### Nine-decimal canonical numeric model

Canonical derived real values use:

```text
Q(v) = round-half-to-even(v, 9 decimal places)
```

Components, temporal proximity, edge weights, final scores, and thresholds are
canonicalized before comparison/ranking/serialization. Canonical reals render
with exactly nine fractional digits and no exponent notation.

### Exact effective-profile validation

Weights are finite non-negative ordinary base-10 decimals with at most nine
fractional digits. Exponent-form authoring is excluded. A component is enabled
iff its effective weight is positive; zero means disabled. Evidence absence does
not alter enabledness.

At least one component is enabled and each exact profile sums to
`1.000000000`; co-change requires `alpha + beta = 1.000000000`. Validation occurs
before `Q`; invalid sums fail rather than being repaired.

### Base graph `G0` is same-commit co-change

The stable evidence/scoring graph contains one edge exactly when two retained
logical files occur in the same canonical file-evidence commit:

```text
E0 = { (a,b) : CommitCoChange(a,b) > 0 }
```

Task co-change can weight an existing edge but cannot create a task-only base
edge in v1. Pair normalization/ranking, distinct-neighbor degree, raw incident
degrees, and `K_f` consume `G0`.

### Threshold graph `Gtheta` cannot feed back into scoring

A significance threshold applies to canonical `CombinedCoChange` with inclusive
`>=` and creates:

```text
Gtheta = (V, { e in E0 : CombinedCoChange(e) >= theta })
```

`Gtheta` exists only for cluster construction and cluster-derived candidate
logic. It cannot change `G0`, normalization, `D_f`, `K_f`, hotspot, bottleneck,
or OCP scores.

### Cluster ranking uses qualifying edges only

For a thresholded connected component, `ClusterMaximum` is the maximum canonical
combined weight among qualifying component edges and `ClusterAggregate` is `Q`
of the sum of those same qualifying edges. Sub-threshold internal `G0` edges do
not contribute.

### Independent task evidence

Multi-reference commits may contribute ordinary breadth/co-change but cannot by
themselves prove independent work. A task pair requires pair-exclusive canonical
file-evidence commits on both sides. Shared-reference commits do not enter pair
intervals.

For a task participating in multiple independent pairs, repeated-edit evidence
unions its pair-exclusive commit sets and deduplicates by SHA before counting,
preventing partner-count multiplication.

### Temporal proximity uses epoch-second gaps, not calendar days

Pair intervals are closed intervals over committer epoch seconds. For
non-overlapping intervals:

```text
gap_seconds = later.start - earlier.end

days_between = 0                         if gap_seconds <= 0
               ceil(gap_seconds / 86400) if gap_seconds > 0

TemporalProximity = Q(1 / (1 + days_between))
```

Alternative considered: calendar-day distance. Rejected because local dates,
timezones, DST, and truncation can change the result. A 25-hour gap is always two
`days_between` under the canonical formula.

### Cohort-safe centrality

Endpoint-cohort-normalized edge scores cannot be summed into one file centrality
value. Centrality uses raw `G0` incident evidence first:

```text
IncidentCommitDegree(f) = Σ CommitCoChange(f,n)
IncidentTaskDegree(f)   = Σ TaskCoChange(f,n)
IC_f = normalized IncidentCommitDegree inside f's file-category cohort
IT_f = normalized IncidentTaskDegree inside f's file-category cohort
K_f  = Q(alpha*IC_f + beta*IT_f)
```

V1 deliberately reuses effective co-change `alpha/beta`; there is no second
hidden centrality mix.

### Portable ASCII role-token evidence

The role-token signal uses an explicit ASCII tokenizer: non-ASCII or
non-alphanumeric delimiters, lowercase→uppercase split,
acronym-final-uppercase-before-lowercase split, letter↔digit split, and ordinal
ASCII lowercase. Matching is exact equality only.

### Canonical JSON escaping and bytes are fully specified

Canonical strings are valid Unicode scalar sequences. V1 Git paths enter that
model only through strict UTF-8 decoding.

String escaping is fixed:

- quote as `\"`;
- backslash as `\\`;
- standard short escapes for backspace/tab/newline/formfeed/carriage-return;
- other U+0000..U+001F controls as `\u00XX` with uppercase hex;
- `/` remains literal;
- every other scalar, including non-ASCII, is emitted directly as UTF-8 rather
  than optional `\uXXXX` escapes.

Canonical arrays follow capability ordering; object properties follow #243's
versioned schema order; dynamic map keys use ascending ordinal order.

Canonical bytes use UTF-8 without BOM, LF, two-space indentation, no trailing
whitespace, one terminal LF, fixed nine-decimal canonical reals, and the escaping
profile above. Report-artifact identity is over those bytes.

## Risks / Trade-offs

- Missing/inconsistent task refs understate signals → preserve zero semantics and
  report the limitation.
- Merge-resolution-only edits are not file-scored → expose excluded merge count.
- Exact-blob rename recognition misses rename-with-edit → accept v1 false negative
  for backend-independent identity.
- Pure exact renames have zero content churn → commit touch remains visible.
- Binary/unavailable line counts have zero churn → preserve an explicit status so
  zero is not mistaken for proven no-content-change.
- Non-UTF8 Git paths fail v1 analysis → deterministic failure beats host-dependent
  canonical strings.
- Category-local normalization prevents noise domination → cross-category scores
  are not globally comparable.
- Task-only co-change does not create `G0` edges → retain it only as weight on a
  same-commit edge.
- Role tokens may overfit names → bounded exact ASCII matching and caveats.

## Verification expectations

Downstream implementation should include at minimum:

- reachability inclusion independent of traversal order;
- root empty-tree delta;
- merge metadata without file double-counting;
- pure 100-line exact rename => one touch, zero additions/deletions/churn;
- binary/unavailable line count => zero counts plus explicit marker;
- commit count => distinct canonical file-evidence commits;
- invalid UTF-8 Git path => deterministic failure;
- exact rename across categories, split, and alias cycle;
- category-isolated normalization;
- half-even and normalized-log vectors;
- task-only episode not creating a base edge;
- threshold changes not affecting `D_f`/`K_f`/file scores;
- cluster aggregate using qualifying edges only;
- multi-reference false-positive controls and repeated-edit SHA union;
- 25-hour gap => `days_between=2`;
- mixed endpoint-category centrality;
- ASCII role-token vectors;
- JSON escaping vector with quote, backslash, slash, control and non-ASCII scalar;
- locale/enumeration-independent canonical JSON bytes;
- empty range/all-zero evidence.

## Migration Plan

1. Archive this theory contract into the main capability.
2. #236 implements reachability, file deltas, merge exclusion, strict UTF-8 paths,
   canonical file events/line-count status, exact rename identity, task refs, and
   the CLI ingestion family.
3. #237 implements validated config, ignores, categories, thresholds, profiles.
4. #238/#239 implement hotspot, `G0`, `Gtheta`, pair, and cluster evidence.
5. #240/#241 implement independent-task, exact temporal-gap, cohort-safe
   centrality, and OCP pressure.
6. #242/#243 add optional enrichment and canonical reports.

No deployment or rollback action is required because this change adds no runtime
behavior.

## Open Questions

- None for the first deterministic profile. Changes to commit-set semantics,
  merge handling, path encoding, rename recognition, file-event/churn semantics,
  graph membership, defaults, numeric scale, temporal-gap formula, tokenizer
  behavior, JSON escaping/byte profile, or evidence semantics require reviewed
  specification work with compatibility/migration notes.
