## Context

History ingestion completes canonical Git evidence, logical-file identity, graph
evidence, and scores before any source analysis occurs. Core already owns
project discovery, verified assembly resolution, and
`ArchitectureSourceFileFactIndex`, but those services read a checkout and its
build artifacts rather than arbitrary Git trees. The enrichment must therefore
prove that those facts describe the analyzed `to` revision before using them.

## Goals / Non-Goals

**Goals:**

- Attach deterministic .NET context to finalized logical-file findings only when
  the analyzed `to` commit is exactly the clean checkout `HEAD`.
- Reuse Core policy loading, project discovery, post-build assembly resolution,
  and source-file fact indexing instead of adding a history-specific parser or
  project scanner.
- Represent not-requested, not-applicable, available, and unavailable outcomes
  without changing a Git-only result, its scores, or its ordering.
- Keep future #243 report rendering as the sole owner of canonical report schema
  and bytes.

**Non-Goals:**

- Checking out, reconstructing, or compiling historical revisions.
- Making compilation, project discovery, source parsing, or Roslyn facts a
  prerequisite for Git ingestion or reportability.
- Changing ref, metadata, TaskKey, path, rename, temporal, graph, scoring, or
  candidate semantics.
- Adding a second report renderer or using enrichment to manufacture candidates.

## Decisions

### Enrich only a verified current revision

The enricher runs only after successful history ingestion. It verifies that the
repository worktree is clean and its exact checked-out `HEAD` equals the
canonical resolved `to` object ID. A mismatch, dirty worktree, unavailable
policy, discovery problem, build-state problem, assembly-load failure, or source
parser failure yields deterministic `unavailable` status rather than a history
failure. This is preferred over optimistic current-worktree mapping because the
latter could silently attach facts from a different revision.

### Reuse the architecture fact pipeline

When the revision guard passes, the enricher loads the selected architecture
policy, discovers projects, resolves verified post-build artifacts in an
isolated scope, and creates an `ArchitectureAnalysisSession`. It reads its
`SourceFileFactIndex` to map each existing canonical `.cs` path to stable
assembly, namespace, and declared-type facts. This reuses Core’s established
source ownership and Roslyn syntax parsing behavior; no history-specific source
scanner is introduced.

### Use logical-file canonical paths as the join key

Each finalized logical file has one canonical path. Enrichment looks up that
exact canonical path only, never reclassifies aliases or recomputes lineage.
Files outside .NET source or with no trustworthy current source fact are
`not_applicable`; a lookup failure for one file is not a repository failure.
Facts and files are ordinally ordered before projection to remain stable under
project/source enumeration permutations.

### Keep enrichment out of Git-only renderers

`HistoryIngestionResult` carries a separately replaceable enrichment projection,
initially `not_requested`. Existing Git-only JSON/text writers remain unchanged;
#243 owns versioned presentation of the projection. This preserves their current
output and makes it impossible for an enrichment error to suppress a completed
Git result.

## Risks / Trade-offs

- [Only a clean checkout at `to` can be enriched] → Report an explicit
  `unavailable` status; callers can analyze Git-only evidence or prepare the
  intended checkout.
- [Policy/project/build setup can fail] → Catch this strictly within enrichment
  and retain the fully completed history result unchanged.
- [A canonical path may be deleted or non-.NET at `to`] → Mark only that file
  `not_applicable`, retaining its Git-level evidence.
- [Existing fact services load assemblies] → Use their isolated post-build
  resolution path and dispose its load scope after fact materialization.

## Migration Plan

No persisted data or report schema changes are made. The history command gains
an explicit opt-in enrichment switch requiring the existing policy input. Users
can remove the switch to receive exactly the existing Git-only behavior.
