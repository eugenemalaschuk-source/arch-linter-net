## Context

Issue #235 defines Release Architecture Forensics theory before implementation.
Downstream tasks need one reviewed source of truth for Git-range identity,
file-touch evidence, rename recognition, normalization, numeric precision, task
independence, graph semantics, ranking, and cautious report language.

The feature is planned, not shipped, so public MkDocs pages must not present it
as current product behavior.

A merge-safety audit of the first completed theory exposed a final lower-level
class of ambiguity: the score formulas were deterministic once evidence existed,
but the contract still allowed different reasonable implementations to choose
different commit sets/deltas, rename inference, graph membership, and cluster
aggregation. Those choices belong in #235 because #236/#239 must consume theory,
not invent foundational evidence semantics ad hoc.

## Goals / Non-Goals

**Goals:**

- deterministic Git-only evidence and canonical scoring;
- one canonical reachability and file-delta model;
- conservative, backend-independent rename recognition;
- explicit comparable normalization cohorts and numeric precision;
- validated effective profiles and threshold semantics;
- explicit base/threshold graph ownership;
- false-positive control for multi-reference task evidence;
- cohort-safe graph centrality and ranking;
- executable cluster aggregation semantics;
- portable role-token and canonical JSON rendering rules;
- optional .NET enrichment strictly downstream.

**Non-Goals:**

- implementing CLI, policy schema, analyzer, reports, or Roslyn enrichment;
- a second config language or executable;
- similarity-based rename heuristics in the first deterministic profile;
- treating merge-resolution-only edits as fully modeled in v1;
- formal proof of coupling, merge conflicts, or OCP violations.

## Decisions

### Internal reference plus capability spec

The OpenSpec capability is the testable contract and
`docs/internal/release-forensics.md` is the readable contributor reference.
Public navigation remains unchanged until implementation ships.

### Canonical reachability range and merge-file policy

The canonical commit set is `Reachable(to) \ Reachable(from)`, with each ref
included in its own reachability set. Commit ordering is committer UTC epoch
second then ordinal full SHA. File-level temporal metrics use the same committer
timestamps.

Ordinary one-parent commits use parent-tree → commit-tree deltas; root commits
use the empty tree. Merge commits remain range metadata but do not contribute
file touches or any file-derived evidence in the first profile.

Alternative considered: first-parent, per-parent, or combined merge diffs.
Rejected because each choice produces different churn/touch/co-change evidence
and can double-count branch changes that already appear independently in the
reachability range. Excluding merge file deltas is conservative and fully
repeatable; the cost is explicitly reported as possible undercounting of
merge-resolution-only edits.

### Exact one-to-one blob rename recognition

A canonical rename exists only when one non-merge commit has a one-to-one
delete/add relation with the same Git blob object ID. Similarity and copy
inference do not participate in canonical identity in the first profile.

Alternative considered: Git-style similarity rename detection. Rejected for v1
because thresholds, candidate limits, copy behavior, backend/library versions,
and ambient Git configuration would become hidden canonical inputs. A later
similarity profile requires reviewed algorithm/threshold/compatibility semantics.

The last in-range path remains canonical. Aliases are distinct non-canonical
historical paths, ordered by first canonical occurrence then ordinal path, with
the canonical path excluded from aliases. Split/copy/many-to-one/rename-with-edit
cases remain separate rather than guessing a chain.

### Comparable normalization cohorts

#237 ignores remove files before base-graph and score population construction.
File metrics normalize within primary-category cohorts; base-edge metrics within
unordered endpoint-category cohorts. Presentation suppression cannot affect
scores.

Because cohort-local normalized values are not a common absolute scale, findings,
pairs, clusters, and candidates remain grouped and rank only within comparable
cohorts. Production is the primary human hotspot ranking.

### Nine-decimal canonical numeric model

Mathematical formulas remain authoritative. Canonical derived reals use:

```text
Q(v) = round-half-to-even(v, 9 decimal places)
```

Components, temporal proximity, edge weights, final scores, and thresholds are
canonicalized before comparison/ranking/serialization. JSON uses exactly nine
fractional digits, invariant culture, no exponent notation. This avoids making
canonical output depend on floating-point/library details after the mathematical
value is established.

### Exact effective-profile validation

Weights are finite non-negative ordinary base-10 decimals with at most nine
fractional digits. Exponent-form authoring is excluded from canonical config.
A component is enabled iff its effective weight is positive; zero means disabled,
and evidence absence never changes enabledness.

At least one component is enabled and each exact profile sums to
`1.000000000`; co-change therefore requires `alpha + beta = 1.000000000`.
Validation occurs before `Q`; invalid sums fail rather than being rounded or
silently normalized.

### Base graph `G0` is same-commit co-change

The stable evidence/scoring graph contains one edge exactly when two retained
logical files occur in the same canonical file-evidence commit:

```text
E0 = { (a,b) : CommitCoChange(a,b) > 0 }
```

Task co-change can weight an existing edge but cannot create a task-only base
edge in the initial profile.

Alternative considered: allow task episodes to create edges even when the files
never changed together. Rejected because that would change neighbor degree,
centrality, and bottleneck/OCP scores even though #239's primary graph signal is
same-commit co-change.

Pair normalization, pair ranking, distinct-neighbor degree, raw incident degrees,
and `K_f` all consume `G0`.

### Threshold graph `Gtheta` cannot feed back into scoring

A significance threshold applies to canonical `CombinedCoChange` with inclusive
`>=` and creates:

```text
Gtheta = (V, { e in E0 : CombinedCoChange(e) >= theta })
```

`Gtheta` exists only for cluster construction and cluster-derived candidate
logic. It cannot change base graph membership, normalization, `D_f`, `K_f`, or
hotspot/bottleneck/OCP scores.

Alternative considered: prune the scoring graph using the cluster threshold.
Rejected because tuning presentation/grouping sensitivity would then silently
change file scores and invalidate score comparability across threshold settings.

### Cluster ranking uses qualifying edges only

For a thresholded connected component, `ClusterMaximum` is the maximum canonical
combined weight among qualifying component edges and `ClusterAggregate` is `Q`
of the sum of those same qualifying edges. Sub-threshold base edges between
members do not contribute.

Alternative considered: sum every internal `G0` edge. Rejected because that
would let an edge that failed cluster qualification still influence cluster rank
and would make cluster aggregation depend on an unstated second graph.

### Independent task evidence

Multi-reference commits may contribute ordinary breadth/co-change but cannot by
themselves prove independent work. A task pair requires pair-exclusive canonical
file-evidence commits on both sides.

Pair intervals are closed intervals from minimum to maximum committer epoch
second of the pair-exclusive commits. Shared-reference commits do not enter those
intervals.

For repeated OCP editing, a task participating in multiple independent pairs
unions its pair-exclusive commit sets and deduplicates by SHA before counting
repeated edits. This prevents partner-count multiplication.

### Cohort-safe centrality

Endpoint-cohort-normalized edge scores cannot be summed into one file centrality
value because a file may have edges from several endpoint-category cohorts.
Therefore centrality uses raw `G0` incident evidence first:

```text
IncidentCommitDegree(f) = Σ CommitCoChange(f,n)
IncidentTaskDegree(f)   = Σ TaskCoChange(f,n)
IC_f = normalized IncidentCommitDegree inside f's primary-category cohort
IT_f = normalized IncidentTaskDegree inside f's primary-category cohort
K_f  = Q(alpha*IC_f + beta*IT_f)
```

The first profile deliberately reuses effective co-change `alpha/beta`; there is
no hidden second centrality mix.

Alternative considered: sum canonical `CombinedCoChange` over incident edges.
Rejected because production-production and production-tests edge scores may have
been normalized against different populations.

### Portable ASCII role-token evidence

The role-token signal uses an explicit ASCII tokenizer: non-ASCII/non-alphanumeric
delimiters, lowercase→uppercase split, acronym-final-uppercase-before-lowercase
split, letter↔digit split, and ordinal ASCII lowercase. Matching is exact token
equality only.

Alternative considered: library-provided Unicode alphanumeric/casing rules.
Rejected because the current default role vocabulary is ASCII and platform or
Unicode-version differences should not alter canonical `N_f`.

### Canonical JSON is a byte profile, not dictionary accident

Canonical JSON arrays follow the capability's stable grouping/order. Object
properties follow #243's versioned report-schema order; dynamic map keys use
ascending ordinal order after canonical string normalization.

Canonical bytes use UTF-8 without BOM, LF line endings, two-space indentation,
no trailing whitespace, exactly one terminal LF, and fixed nine-decimal real
formatting without exponent notation. Report-artifact identity is defined over
those bytes rather than incidental in-memory map ordering.

### Optional .NET enrichment stays downstream

Project/namespace/type facts enrich completed file findings but cannot remove,
reorder, or manufacture Git evidence.

## Risks / Trade-offs

- Missing/inconsistent task refs understate signals → preserve zero semantics and
  report the limitation.
- Merge-resolution-only edits are not file-scored in v1 → expose excluded merge
  count and state the limitation rather than choosing an unstable merge-diff mode.
- Exact-blob rename recognition misses rename-with-edit → accept the false
  negative for v1 in exchange for backend-independent canonical identity.
- Category-local normalization prevents noise domination → cross-category scores
  cannot be interpreted as one global ranking.
- Different math libraries may vary internally → only correctly rounded
  nine-decimal canonical values participate in decisions/output.
- Multi-reference commits may represent legitimate shared work → preserve
  ordinary breadth while requiring pair-exclusive evidence for parallel pressure.
- Cluster threshold choice influences grouping → isolate it in `Gtheta` so it
  cannot feed back into base scoring.
- Task-only co-change can reveal process coupling but does not create `G0` edges
  in v1 → retain it as edge weight only where same-commit coupling exists.
- Role tokens may overfit names → exact bounded ASCII matching and caveats.

## Verification expectations

Downstream implementation should convert the theory into synthetic/golden tests,
including at minimum:

- reachability inclusion independent of traversal order;
- root empty-tree delta;
- merge metadata without file double-counting;
- exact rename across categories;
- rename split/cycle alias handling;
- task-only episode not creating a base edge;
- threshold changes not affecting `D_f`/`K_f`/file scores;
- cluster aggregate using qualifying edges only;
- multi-reference false-positive controls and repeated-edit SHA union;
- mixed endpoint-category centrality;
- ASCII role-token vectors (`OrderService`, `XMLParser2`, `Serviceable`);
- half-even and normalized-log numeric vectors;
- locale/enumeration-independent canonical JSON bytes;
- empty range/all-zero evidence.

## Migration Plan

1. Archive this theory contract into the main capability.
2. #236 implements reachability, file deltas, merge exclusion, exact rename
   identity, task refs, and the CLI ingestion family.
3. #237 implements validated config, ignores, categories, thresholds, profiles.
4. #238/#239 implement canonical hotspot, `G0`, `Gtheta`, pair, and cluster
   evidence.
5. #240/#241 implement independent-task, cohort-safe centrality, and OCP pressure.
6. #242/#243 add optional enrichment and stable canonical reports.

No deployment or rollback action is required because this change adds no runtime
behavior.

## Open Questions

- None for the first deterministic profile. Changes to commit-set semantics,
  merge handling, rename recognition, graph membership, defaults, numeric scale,
  tokenizer behavior, report-byte profile, or evidence semantics require reviewed
  specification work with compatibility/migration notes.
