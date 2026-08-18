# Design

## Context

#235 fixed the theory; #236 must make it executable without inheriting a Git
client's presentation semantics. The dominant design constraint is that every
canonical decision — object IDs, ref resolution, identity grammar, epoch integers,
paths, rename lineage, and line churn — must be reproducible from raw repository
bytes, independent of the host's Git installation, locale, calendar, and diff
configuration.

## Goals / Non-Goals

Goals:

- One deterministic ingestion pipeline that reads the repository object database
  directly and fails closed on anything it cannot canonically interpret.
- One CLI entry point that either prints a complete ingestion result or prints a
  diagnostic and prints no result at all.
- Evidence shaped so #237–#244 can consume it without re-deriving Git semantics.

Non-Goals:

- Scoring, co-change graphs, clusters, candidates, or the versioned #243 report
  schema.
- Git revision-expression compatibility, similarity rename detection, merge-delta
  evidence, or Roslyn enrichment.

## Decisions

### Read the object database directly instead of shelling out to `git`

Invoking `git` would make canonical evidence depend on the host binary's version,
`diff` configuration, `core.quotepath`, attribute files, textconv, and locale. The
pipeline therefore reads loose objects and packfiles itself: zlib-inflated loose
objects, pack `.idx` v2 lookup, and `OBJ_OFS_DELTA`/`OBJ_REF_DELTA` reconstruction.
This also makes SHA-1 and SHA-256 repositories a single code path parameterized by
digest length read from `extensions.objectformat`.

Cost: a pack reader is real code to own. It is accepted because the alternative
cannot satisfy "identical canonical bytes in different environments".

### Keep the pipeline internal to Core

`ArchLinterNet.Core` already exposes its internals to `ArchLinterNet.Cli` through a
reviewed friend-assembly rule. Keeping every ingestion type `internal` means the
first executable slice ships without widening the reviewed public API surface, which
would otherwise freeze provisional shapes that #237–#243 are expected to extend.

### Fail closed through a result type, not exceptions

Every fail-closed condition in #235 is a *diagnostic* with a stable kind and optional
object/path/span identity. Modeling it as a returned result keeps the "no partial
report" rule structural: the CLI cannot accidentally print a half-built result while
unwinding, because a failed ingestion never produces a result object at all.

### Order candidates by ancestry before checking the chain

The lineage rule asks for *exactly one* permutation of a component's candidates that
is ancestry-ordered, endpoint-linked, and lifecycle-clean. Enumerating permutations
is factorial. Instead the implementation first requires the component's candidate
commits to be pairwise strictly ancestry-comparable — which fixes the permutation
uniquely — and then checks endpoint linking and the lifecycle guard on that single
ordering. Any incomparable pair, any pair of candidates in the same commit, a broken
link, or an intervening add/delete makes the component `ambiguous_dag`. This yields
the specified outcome for the `A -> B -> A` alias cycle, which has no
lifecycle-free start vertex but is uniquely ordered by ancestry.

### LCS length with common-affix reduction

Canonical churn depends only on the mathematical LCS *length*, so the implementation
is free to use any algorithm producing that length. Lines are hashed to integers,
common prefix and suffix runs are removed (a reduction that provably preserves LCS
length), and the remainder uses a two-row dynamic program. No diff script is ever
materialized, which is exactly why the totals cannot depend on tie-breaking.

## Risks / Trade-offs

- **Pack reading correctness**: a subtle delta bug would silently corrupt evidence.
  Mitigated by testing against real repositories created by `git` itself, so any
  divergence from Git's own object encoding surfaces as a test failure.
- **Full-history reachability cost**: `Reachable(to) \ Reachable(from)` and the
  ancestry checks traverse the commit DAG rather than a bounded window. Accepted for
  v1: correctness of the range definition outranks traversal cost, and commit-object
  parsing is cached per run.
- **Tree diffing cost**: subtree pairs with identical object IDs are skipped, so the
  per-commit delta cost tracks the size of the change rather than the size of the
  tree.

## Migration Plan

Additive only. No existing command, policy field, schema, or public API changes, so
there is nothing to migrate.

## Open Questions

None blocking. The successful report schema, configuration surface, and scoring stay
owned by #237, #238, and #243 respectively.
