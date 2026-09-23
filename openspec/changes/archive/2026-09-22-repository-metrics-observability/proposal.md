## Why

ArchLinterNet already has the authoritative project graph, source/type facts, and unified architecture PR report, but it does not expose a compact, neutral snapshot of repository size and structural complexity. Issue #990 adds low-cost observability for adopters and reviewers while the v0.9 performance/scale work is active, without turning those measurements into governance or adding a second analysis pass.

## What Changes

- Add a typed repository-metrics snapshot derived from the existing analysis snapshot and canonical project graph.
- Define deterministic size, coupling, fan-in/fan-out, instability, SCC, and dependency-depth semantics, including trivial repositories, cycles, duplicate source-file identities, and unavailable evidence.
- Expose the absolute head snapshot through the supported human and machine-readable reporting surfaces.
- Extend the existing Core-owned unified PR report with a bounded base-to-head Repository metrics delta section when compatible base evidence exists; render unavailable base evidence explicitly.
- Add a compact absolute Source lines badge projection through the existing trusted default-branch badge publication boundary, without using PR deltas or changing Architecture Health.
- Add regression, determinism, graph-edge, source-identity, report, badge, and performance-boundary evidence; keep metrics informational and outside strict/audit outcomes, findings, exit codes, and metric budgets.

## Capabilities

### New Capabilities

- `repository-metrics-observability`: Typed repository size/coupling/structure snapshots, absolute reporting, compatible PR deltas, and neutral absolute badge projection.

### Modified Capabilities

- `architecture-pr-reporting`: Add the bounded, neutral Repository metrics delta projection to the existing canonical PR report.
- `architecture-pr-report-publication`: Preserve the existing single sticky publication path while transporting the extended Core-rendered report.

## Impact

- Core analysis snapshot, project/dependency graph, source-file fact, report-model, and JSON serialization seams.
- Core-owned PR report projection and CLI Markdown renderer.
- Existing badge projection/publication contracts and the verified-main workflow, with no new publisher or privileged execution path.
- NUnit Core/CLI tests, performance evidence, OpenSpec specs, and user-facing observability documentation.
- No new NuGet dependency, MSBuild load, semantic compilation, repository traversal, governance finding, or Architecture Health dimension.
