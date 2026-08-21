## Context

The canonical ingestion pipeline already establishes logical-file identity,
canonical TaskKeys and match provenance, `G0`, and bottleneck independent-pair
evidence. Existing OCP weights are validated in configuration, but the result
model and canonical report currently stop at bottleneck analysis.

## Goals / Non-Goals

**Goals:**

- Produce one deterministic OCP-pressure finding per canonical logical file.
- Reuse settled independent-pair and `G0` evidence without reinterpreting Git,
  TaskKey, path-lifetime, or rename semantics.
- Keep evidence auditable in the in-memory result and canonical JSON.

**Non-Goals:**

- Prove an Open/Closed Principle violation or recommend an automatic refactor.
- Add Unicode/culture-aware tokenization, Roslyn type analysis, path-lifetime
  segmentation, or `Gtheta`-based scoring.
- Change policy configuration, CLI arguments, or existing bottleneck behavior.

## Decisions

### Reuse canonical bottleneck pairs as the independence source

The OCP scorer will consume each logical file's existing independent TaskKey
pairs and file-event commits. This preserves the established pair-exclusive
definition and its TaskKey provenance. Recomputing independence in a second
implementation was rejected because it could drift from bottleneck semantics.

### Calculate repeated editing per key with a SHA set

For every task with independent partners, the scorer will union that task's
pair-exclusive commit IDs across all partners, then use `max(count - 1, 0)`.
This directly prevents a shared qualifying commit or additional partners from
multiplying pressure.

### Share raw G0 centrality calculation, not thresholded graph evidence

The score derives incident commit/task degrees from `CoChangeGraph.BaseEdges`
and applies the existing co-change weights inside each file category. This is
the same cohort-safe `K_f` semantics used for bottlenecks and deliberately
excludes clusters and significance thresholds.

### Use an isolated fixed ASCII filename tokenizer

The role-hint helper tokenizes only the final filename stem and compares exact
lowercase ASCII tokens to the fixed default set. This keeps the portability
contract local and avoids culture or substring matching surprises.

### Extend existing canonical report projections

The result gains an OCP analysis beside bottlenecks, and the canonical JSON
writer emits groups, raw evidence, normalized components, and effective weights.
No public API is required because these types remain internal Core history
implementation details.

## Risks / Trade-offs

- [Same-path reuse can conflate generations] → Preserve and expose the existing
  pathname-reuse limitation on every finding.
- [Independent-pair code could be duplicated] → Consume the bottleneck pair
  evidence instead of recreating pair logic.
- [Role hints can overgeneralize filenames] → Report them as bounded heuristic
  token matches and leave non-matches at zero.
- [Report-schema growth affects byte snapshots] → Add focused canonical JSON
  assertions and keep writer property order explicit.
