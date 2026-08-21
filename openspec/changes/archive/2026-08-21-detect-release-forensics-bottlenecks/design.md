## Context

The history pipeline already settles canonical commits, TaskKeys, logical-file identities, and `G0` co-change evidence before constructing result projections. `HistoryHotspotScorer` and `CoChangeGraphBuilder` establish the existing pattern: immutable analysis objects are attached to `HistoryIngestionResult` and written by the intermediate deterministic JSON writer.

## Goals / Non-Goals

**Goals:**

- Project pair-exclusive TaskKey evidence into deterministic per-file bottleneck findings.
- Use arbitrary-precision epoch integers for interval arithmetic and preserve canonical source provenance.
- Derive centrality only from raw `G0` incident counts and normalize every file component inside its primary-category cohort.
- Make findings inspectable through the existing result and JSON surfaces.

**Non-Goals:**

- Changing Git ingestion, TaskKey extraction, rename resolution, baseline identities, `G0` topology, or `Gtheta` clusters.
- Claiming merge conflicts or splitting same-path delete/re-add lifetimes.
- Implementing OCP pressure or the final versioned report renderer.

## Decisions

### Add a dedicated Core analysis projection

Introduce a focused bottleneck scorer and immutable finding/evidence model next to hotspot and co-change analysis. This keeps the algorithm inside the Core history boundary and prevents reporting code from reconstructing evidence. Extending `CoChangeGraphBuilder` would conflate graph construction with a consumer-specific score, while deriving it in the JSON writer would make rendering semantic.

### Bind file commits to settled commit evidence once

The scorer indexes canonical `CommitEvidence` by file-event commit ID, then evaluates each unordered TaskKey pair from that fixed evidence. A pair side includes commits that contain its key and omit its partner; multi-reference commits remain ordinary breadth evidence but cannot establish pair independence or extend its interval. This retains exact TaskKey provenance and does not revisit raw messages.

### Use BigInteger temporal intervals and shared nine-decimal quantization

Committer epoch seconds are parsed into `BigInteger`; endpoint selection and gap arithmetic are therefore independent of host date/time limits and timezone tokens. Ratios and final scores use decimal half-even quantization at nine digits, with integer normalization inputs bounded by existing file/graph evidence.

### Compute centrality from raw G0 evidence

For each graph vertex, sum `CommitCoChange` and `TaskCoChange` over `BaseEdges`, calculate distinct neighbor degree, and normalize those raw values within the vertex category. `Gtheta` and edge-normalized components are not read. This preserves threshold invariance and avoids mixing endpoint cohorts.

### Extend the interim JSON writer only

The writer receives finalized `HistoryBottleneckAnalysis` through `HistoryIngestionResult` and serializes weights, category groups, components, source matches, intervals, authors, and identity limitations. This is an intermediate evidence surface; #243 remains responsible for the versioned report schema.

## Risks / Trade-offs

- [Pair count grows quadratically in TaskKeys per file] → canonical release ranges are bounded by explicit input; use one ordered key set and direct pair iteration.
- [Nine-decimal rounding around temporal ratios] → compute `1/(1+days)` with deterministic decimal arithmetic and assert the 90,000-second vector.
- [Synthetic tests bypass ingestion semantics] → combine scorer-focused synthetic `G0` tests with Git-backed canonical TaskKey and timestamp tests.
- [Future report contract needs more detail] → preserve pair-level keys, source matches, exact intervals/gaps, authors, components, and effective weights now.
