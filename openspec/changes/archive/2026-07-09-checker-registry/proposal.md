## Why

Every contract family currently needs three hand-written pieces just to execute: a catalog descriptor entry, a one-line `IArchitectureContractHandler` class in `ArchitectureContractHandlers.cs` that forwards to a `session.CheckXxxContract` method, and a matching `services.AddSingleton<IArchitectureContractHandler, XxxHandler>()` line in `ServiceCollectionExtensions.cs`. The handler class and the DI line carry no behavior beyond routing — the descriptor registry introduced for #208/#209 already gives each family a single home for its catalog metadata, but checker behavior still lives in two separate places. This is issue #211: fold checker dispatch into the descriptor so a new family is one registry entry, not three edits across two files.

## What Changes

- Add a non-generic `ArchitectureContractChecker` delegate type (`ArchitectureAnalysisSession`, `IArchitectureContract` → `ArchitectureHandlerResult`) that replaces `IArchitectureContractHandler` as the checker contract shape.
- Add a `Checker` property to `ArchitectureContractFamilyDescriptor` and populate it for all 25 entries in `ArchitectureContractFamilyRegistry.All`, moving each handler's one-line forwarding body into its descriptor.
- **BREAKING (internal API)**: Delete `ArchitectureContractHandlers.cs` (all 24 handler classes) and the `IArchitectureContractHandler` interface. Nothing outside this assembly implements or consumes these types today.
- Rebuild `ArchitectureContractHandlerRegistry` to source its family → checker map directly from `ArchitectureContractFamilyRegistry.All` instead of a DI-injected `IEnumerable<IArchitectureContractHandler>`. Public dispatch API (`TryGetHandler`, `Execute`) keeps its shape and behavior.
- Remove all 24 `services.AddSingleton<IArchitectureContractHandler, ...>()` lines from `ServiceCollectionExtensions.cs`; registering the handler registry becomes a single parameterless singleton registration.
- No change to `ArchitectureContractExecutor`'s dispatch loop, execution order, strict/audit mode selection, coverage's separate summary bucket, the asmdef CLI inclusion toggle, baseline candidate collection, unmatched-ignore tracking, or the `[id] ` prefix on cycle/acyclic_sibling results — all of that is unaffected by where the checker delegate lives.

## Capabilities

### New Capabilities
(none — this is a wiring change inside two existing capabilities)

### Modified Capabilities
- `contract-handler-execution`: the handler abstraction becomes a descriptor-owned `ArchitectureContractChecker` delegate instead of a DI-registered `IArchitectureContractHandler` implementation; the registry is sourced from `ArchitectureContractFamilyRegistry.All`, not from `IEnumerable<IArchitectureContractHandler>`; the composition root no longer registers one handler class per family.
- `contract-family-registry`: `ArchitectureContractFamilyDescriptor` gains a live `Checker` delegate that `ArchitectureContractHandlerRegistry` resolves and invokes for every family (replacing the previously-inert extension surface described for future decomposition — that decomposition is this change).

## Impact

- `src/ArchLinterNet.Core/Execution/ArchitectureContractFamilyDescriptor.cs` — new `Checker` property.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractFamilyRegistry.cs` — every descriptor gains a `Checker` delegate.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractHandlers.cs` — deleted.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractHandlerRegistry.cs` — reworked construction source.
- `src/ArchLinterNet.Core/Execution/Abstractions/IArchitectureContractHandler.cs` — `IArchitectureContractHandler` interface removed; `ArchitectureContractChecker` delegate and `ArchitectureHandlerResult` record added/kept in its place.
- `src/ArchLinterNet.Core/Execution/Abstractions/IArchitectureContractHandlerRegistry.cs` — unchanged shape, revalidated against the new construction path.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs` — 24 registration lines removed.
- `tests/ArchLinterNet.Core.Tests/ArchitectureContractHandlerRegistryTests.cs` — `CreateRegistry()` rewritten to build from the descriptor registry instead of a hand-curated handler list.
- `tests/ArchLinterNet.Core.Tests/ArchitectureContractFamilyRegistryTests.cs` — placeholder/inert assertions about `Checker`/decomposition updated to reflect that checkers are now live.
- `openspec/specs/contract-handler-execution/spec.md`, `openspec/specs/contract-family-registry/spec.md` — requirements updated via delta specs in this change.
