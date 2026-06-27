## Why

Contract checks call `ArchitectureTypeScanner` and `ArchitectureReferenceScanner` directly from `ArchitectureContractRunner.Checking.cs`, with no caching: every `CheckContract`/`CheckLayerContract`/`CheckCycleContract` call re-enumerates all loadable types across target assemblies via reflection, and re-walks a type's interfaces/base types/fields/properties/methods/constructors via reflection, even when a prior contract check already scanned the same type. This couples handlers to static scanners and makes future performance work (caching, parallelism) invasive, since there is no shared place to hold scan results across a validation run.

## What Changes

- Introduce `ArchitectureAnalysisSession`, created once per validation run (in `ArchitectureRunnerFactory.BuildRunner`) and threaded into `ArchitectureContractRunner`, wrapping the resolved assemblies and repository context already held by `ArchitectureAnalysisContext`.
- Introduce `ArchitectureTypeIndex`: lazily memoizes the full set of loadable types across target assemblies once per session, with layer-aware lookup replacing repeated `ArchitectureTypeScanner.FindTypesInLayer`/`GetLoadableTypes` reflection walks.
- Introduce `ArchitectureReferenceGraph`: lazily memoizes `ArchitectureReferenceScanner.GetReferencedTypes(type)` results per type for the lifetime of the session, so repeated lookups for the same type across contract checks reuse the cached result.
- Introduce a layer index helper backed by `ArchitectureTypeIndex` for layer-aware type/containing-layer lookups, reusable by handlers.
- Migrate `DependencyContractHandler`, `LayerContractHandler`, `CycleContractHandler` (and the corresponding `CheckContract`, `CheckLayerContract`, `CheckCycleContract` methods on `ArchitectureContractRunner`) to consume the session/index abstractions instead of calling the static scanners directly.
- Leave `allow_only`, `method_body`, `asmdef`, `independence`, `protected`, `external`, and `acyclic_sibling` checks on direct scanner calls — out of scope for this change, scanners remain available for them.

No **BREAKING** changes — `ArchitectureContractRunner`'s public surface (`CheckContract`, `CheckLayerContract`, `CheckCycleContract`, etc.) keeps its existing signatures and behavior; this is an internal caching/indirection change only.

## Capabilities

### New Capabilities
- `analysis-session-indexes`: the internal per-run session abstraction (`ArchitectureAnalysisSession`, `ArchitectureTypeIndex`, `ArchitectureReferenceGraph`) that contract handlers use for memoized type/reference/layer lookups instead of invoking scanners directly on every check.

### Modified Capabilities
(none — `dependency-contracts`, `layer-contracts`, and `cycle-contracts` describe externally observable validation behavior, which this change does not alter; results are byte-for-byte identical before and after)

## Impact

- Affected code: `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisContext.cs`, `ArchitectureContractRunner.cs`, `ArchitectureContractRunner.Checking.cs`, `ArchitectureRunnerFactory.cs`, `ArchitectureContractHandlers.cs`; new files under `src/ArchLinterNet.Core/Execution/` (or a new `Analysis` folder) for the session/index types.
- No YAML schema changes, no CLI argument/exit-code changes, no public API signature changes.
- Scanners (`ArchitectureTypeScanner`, `ArchitectureReferenceScanner`) remain in place and are still called directly by the handler families not migrated in this change.
