## Why

Contract execution is split between two mechanisms: `dependency`, `layer`, `cycle`, and `coverage` route through `IArchitectureContractHandler` implementations resolved from `ArchitectureContractHandlerRegistry`, while `allow_only`, `method_body`, `asmdef`, `independence`, `protected`, `external`, and `acyclic_sibling` are invoked as direct `runner.CheckXxx()` calls inline in the static `ArchitectureContractExecutor`. The registry itself is populated by a static `CreateDefault()` factory rather than DI. Issue #137 asks to finish the handler/checker pattern for every family and make the registry a DI-populated instance service, so the post-coverage family expansion in #104 can add a family by registering a handler instead of editing a god executor or a static default-handler list.

## What Changes

- Add `IArchitectureContractHandler` implementations for the 7 remaining families: `allow_only`, `method_body`, `asmdef`, `independence`, `protected`, `external`, `acyclic_sibling`. Each delegates to the existing `ArchitectureContractRunner.CheckXxxContract` method, matching the existing `DependencyContractHandler`/`LayerContractHandler`/`CycleContractHandler`/`CoverageContractHandler` pattern in `ArchitectureContractHandlers.cs`.
- Change `ArchitectureContractHandlerRegistry` from a static-`CreateDefault()`-populated lookup into an instance service constructed from `IEnumerable<IArchitectureContractHandler>`, keyed by `Family`. Remove `CreateDefault()`.
- Register all 11 handlers in `ServiceCollectionExtensions.AddArchLinterNetCore()` via `services.AddSingleton<IArchitectureContractHandler, XxxHandler>()`, and register the registry itself as a singleton built from the registered handlers.
- Update `ArchitectureContractExecutor` to dispatch every family (including the 7 newly migrated ones) through the registry instead of calling `runner.CheckXxx()` directly. `ArchitectureContractExecutor` and the runner's `CheckXxx` methods are unchanged in behavior — only the dispatch path changes.
- Thread the registry instance into `ArchitectureContractExecutor`/`ArchitectureRunnerSetupService` (or equivalent execution entry point) so it is built once by the composition root and passed in, rather than constructed ad hoc per run.
- No handler, the registry, or the runner gains a dependency on `IServiceProvider`.

## Capabilities

### New Capabilities
- `contract-handler-execution`: every contract family executes through an `IArchitectureContractHandler`, registered via DI and resolved through a registry instance built from `IEnumerable<IArchitectureContractHandler>`, with no static default-handler factory and no `IServiceProvider` dependency anywhere in the execution path.

### Modified Capabilities
- (none — this change does not alter the observable behavior of any existing contract family's spec; `dependency-contracts`, `layer-contracts`, `cycle-contracts`, `architecture-coverage-model`, `allow-only-contracts`, `method-body-contracts`, `asmdef-contracts`, `independence-contracts`, `external-dependency-contracts`, `acyclic-sibling-contracts` keep their existing requirements as-is — only the internal dispatch mechanism changes)

## Impact

- Affected code: `src/ArchLinterNet.Core/Execution/ArchitectureContractHandlers.cs` (new handlers), `ArchitectureContractHandlerRegistry.cs` (instance service), `ArchitectureContractExecutor.cs` (dispatch via registry for all families), `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs` (handler + registry registrations).
- No changes to `ArchitectureContractRunner`'s `CheckXxxContract` method bodies, to YAML/contract schema, or to CLI/Testing/Unity call sites.
- New/updated tests under `tests/ArchLinterNet.Core.Tests/ArchitectureContractHandlerRegistryTests.cs` covering the newly migrated families and the DI-populated registry construction.
