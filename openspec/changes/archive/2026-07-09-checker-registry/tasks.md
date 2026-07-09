## 1. Checker abstraction

- [x] 1.1 Add the `ArchitectureContractChecker` delegate type (`ArchitectureAnalysisSession session, IArchitectureContract contract` → `ArchitectureHandlerResult`) to `src/ArchLinterNet.Core/Execution/Abstractions/IArchitectureContractHandler.cs`, keeping `ArchitectureHandlerResult` (and its `FromViolations`/`FromCycles` helpers) in place.
- [x] 1.2 Remove the `IArchitectureContractHandler` interface from that file; rename the file to `ArchitectureContractChecker.cs` if that better reflects its new contents.
- [x] 1.3 Add a `Checker` property (type `ArchitectureContractChecker`) to `ArchitectureContractFamilyDescriptor` in `src/ArchLinterNet.Core/Execution/ArchitectureContractFamilyDescriptor.cs`, making it a required part of construction (not an inert optional like `OwnedContractTypes`/`AdditionalValidation`).

## 2. Move checker bodies onto descriptors

- [x] 2.1 For each of the 25 entries in `ArchitectureContractFamilyRegistry.All` (`src/ArchLinterNet.Core/Execution/ArchitectureContractFamilyRegistry.cs`), set `Checker` to a lambda containing the corresponding handler class's one-line body from `ArchitectureContractHandlers.cs` (cast to the concrete contract type, call the matching `session.CheckXxxContract`, wrap via `ArchitectureHandlerResult.FromViolations`/`FromCycles`).
- [x] 2.2 For the `cycle` and `acyclic_sibling` families, preserve the `[id] ` prefix logic on each returned cycle string exactly as `CycleContractHandler`/`AcyclicSiblingContractHandler` do today.
- [x] 2.3 For the `layer_template` descriptor, set `Checker` to the same logic as the `layer` descriptor's `Checker` (cast to `ArchitectureLayerContract`, call `session.CheckLayerContract`) — this replaces the `layer_template → layer` alias that currently lives in `ArchitectureContractHandlerRegistry`'s constructor.
- [x] 2.4 Delete `src/ArchLinterNet.Core/Execution/ArchitectureContractHandlers.cs`.

## 3. Registry rebuild

- [x] 3.1 Change `ArchitectureContractHandlerRegistry` (`src/ArchLinterNet.Core/Execution/ArchitectureContractHandlerRegistry.cs`) to build its family-keyed dictionary by iterating `ArchitectureContractFamilyRegistry.All` and reading each descriptor's `Checker`, instead of taking `IEnumerable<IArchitectureContractHandler>` in its constructor.
- [x] 3.2 Remove the now-unneeded `layer_template → layer` alias wiring from the constructor (subsumed by task 2.3).
- [x] 3.3 Keep `TryGetHandler`/`Execute` method names and behavior (including throwing `InvalidOperationException` for unknown families) on `ArchitectureContractHandlerRegistry` and `IArchitectureContractHandlerRegistry` (`src/ArchLinterNet.Core/Execution/Abstractions/IArchitectureContractHandlerRegistry.cs`), adjusting the `TryGetHandler` out-parameter type to match the new checker delegate.

## 4. Composition root

- [x] 4.1 Remove all 24 `services.AddSingleton<IArchitectureContractHandler, XxxHandler>()` lines from `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`.
- [x] 4.2 Update the `ArchitectureContractHandlerRegistry`/`IArchitectureContractHandlerRegistry` registrations there to construct the registry with no per-family arguments (it self-sources from `ArchitectureContractFamilyRegistry.All`).

## 5. Tests

- [x] 5.1 Rewrite `CreateRegistry()` in `tests/ArchLinterNet.Core.Tests/ArchitectureContractHandlerRegistryTests.cs` to construct `ArchitectureContractHandlerRegistry` via the descriptor-sourced path instead of a hand-curated list of handler instances; remove now-nonexistent handler class references.
- [x] 5.2 Confirm all existing behavior-matching tests in that file (dependency, layer, cycle, allow_only, assembly_independence, assembly_dependency, assembly_allow_only, acyclic_sibling, executor routing/order/baseline/ignore tests) still pass unmodified in assertions — this satisfies the "at least two representative families execute through the new registry path" acceptance criterion many times over.
- [x] 5.3 Update `tests/ArchLinterNet.Core.Tests/ArchitectureContractFamilyRegistryTests.cs`: the `All_NoDescriptorInvokesAdditionalValidationInThisChange` test stays as-is (still about `AdditionalValidation`, not `Checker`); add a new test asserting every descriptor's `Checker` is non-null.
- [x] 5.4 Add/confirm a test exercising `ArchitectureContractHandlerRegistry.Execute` for two or more families end-to-end through descriptor-resolved checkers with no manually-constructed handler list involved (e.g. built via `AddArchLinterNetCore()` DI or the registry's parameterless constructor).

## 6. Validation

- [x] 6.1 Run `make fmt`.
- [x] 6.2 Run `make acceptance` and confirm it is green (includes the self-architecture policy check that `ArchLinterNet.Core.Execution` still doesn't reference `Microsoft.Extensions.DependencyInjection`).
- [ ] 6.3 Run `openspec validate --all` after archiving to confirm the updated `contract-handler-execution` and `contract-family-registry` specs are well-formed.
