## Why

Today, `ArchitectureValidationApplicationService.Validate` performs one full policy composition, project discovery, assembly resolution/load, and `ArchitectureAnalysisSession` construction per call, keyed to exactly one mode (`strict` or `audit`). A caller that wants strict, audit, and coverage together — the common CLI/Testing API/CI shape — invokes `Validate` more than once and pays for policy composition, project-graph evaluation, and assembly loading repeatedly, even though those facts do not depend on which mode is being evaluated. Issue #363 requires that every requested strict/audit/coverage view for one validation session come from one immutable, verified fact set instead.

## What Changes

Introduce an explicit, owned `ArchitectureAnalysisSnapshot` that composes policy, evaluates the selected project graph, loads target assemblies, and runs build-state preflight exactly once, then lets any number of requested modes (`strict`, `audit` — coverage rides inside each mode via the existing `strict_coverage`/`audit_coverage` families) be evaluated against that one snapshot. The existing single-mode `Validate` call becomes a thin wrapper over `CreateSnapshot` + `Evaluate` + `Dispose`, so single-mode behavior, performance, and results are unchanged. CLI and Testing API consumers gain an explicit way to own one snapshot across multiple mode evaluations; ordinary single-mode CLI/Testing usage keeps working exactly as before with no snapshot management required.

## Capabilities

### New Capabilities
- `analysis-snapshot`: an immutable, explicitly owned analysis snapshot (`ArchitectureAnalysisSnapshot`) that composes policy, evaluates the project graph, loads assemblies, and runs build-state preflight once per session, then serves `strict`/`audit` mode evaluations from that one fact set with typed counters, deterministic ordering, and explicit CLI/Testing ownership and disposal.

### Modified Capabilities
(none — existing `Validate` behavior for a single mode is preserved unchanged; no existing spec's requirements change)

## Impact

- `src/ArchLinterNet.Core/Validation/`: new `AnalysisSnapshotRequest`, `ArchitectureAnalysisSnapshotCounters`, `ArchitectureAnalysisSnapshot`; `ArchitectureValidationApplicationService` refactored internally to build snapshots.
- `src/ArchLinterNet.Core/Validation/Abstractions/IArchitectureValidationApplicationService.cs`: new `CreateSnapshot` method.
- `src/ArchLinterNet.Testing/`: new explicit shared-snapshot entry point on `ArchitectureValidationBuilder`.
- `src/ArchLinterNet.Cli/`: `validate` command's `--mode` option accepts a comma-separated mode list, building one snapshot per invocation.
- No change to policy schema, diagnostic shape, or single-mode results.
