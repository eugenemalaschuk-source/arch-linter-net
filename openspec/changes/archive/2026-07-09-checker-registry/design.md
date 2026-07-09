## Context

`ArchitectureContractFamilyRegistry.All` (landed for #208/#209) already gives every contract family one place to declare its catalog metadata: family id, YAML group names, baseline capability, and strict/audit contract accessors. Checker *behavior*, however, still lives outside that registry: `ArchitectureContractHandlers.cs` has one `IArchitectureContractHandler` class per family that casts the contract and forwards to a `session.CheckXxxContract(...)` method, and `ServiceCollectionExtensions.cs` has one `services.AddSingleton<IArchitectureContractHandler, XxxHandler>()` line per family so `ArchitectureContractHandlerRegistry` (built from `IEnumerable<IArchitectureContractHandler>`) can find it. Adding a family today means touching three files (registry entry, handler class, DI line) for logic that is a single forwarding call.

`ArchitectureContractFamilyDescriptor` already carries an `OwnedContractTypes`/`AdditionalValidation` extension surface explicitly reserved as inert "for future family-decomposition tasks (see issue #208)". This change is that future task: attach the actual checker delegate to the descriptor.

## Goals / Non-Goals

**Goals:**
- Collapse the three-file "add a family" workflow into one: append a descriptor with a `Checker` delegate to `ArchitectureContractFamilyRegistry.All`.
- Keep `ArchitectureContractExecutor`'s dispatch loop, family iteration order, and every mode/coverage/asmdef/baseline/diagnostics behavior byte-for-byte identical.
- Keep the registry's public dispatch surface (`TryGetHandler`, `Execute`) stable enough that `ArchitectureContractExecutor` and other consumers need no changes.

**Non-Goals:**
- No runtime/external plugin loading for checkers.
- No YAML contract schema or semantic changes.
- No rewrite of the `session.CheckXxxContract` methods themselves — only where the one-line call to them lives.
- Not reintroducing a generic `IArchitectureContractChecker<TContract>` per-family class hierarchy — that would just replace one boilerplate class hierarchy with another.

## Decisions

### 1. Checker as a non-generic delegate, not a generic interface

The issue text offers `IArchitectureContractChecker<TContract>` as an example, with "or an equivalent non-generic adapter if that better fits." The session's check methods (`CheckContract(ArchitectureDependencyContract)`, `CheckLayerContract(ArchitectureLayerContract)`, `CheckCycleContract(ArchitectureCycleContract)` returning `IReadOnlyCollection<string>` instead of violations, etc.) aren't a uniform generic shape — cycle-shaped families return cycles-with-id-prefixing, coverage-shaped families also produce a summary. A generic `IArchitectureContractChecker<TContract>` would still need one adapter implementation per family to normalize these into `ArchitectureHandlerResult`, which is exactly the boilerplate being removed. A single non-generic delegate:

```csharp
public delegate ArchitectureHandlerResult ArchitectureContractChecker(
    ArchitectureAnalysisSession session, IArchitectureContract contract);
```

lets every family's `Checker` be a lambda assigned inline in the registry list — no class, no DI line. The internal cast to the concrete contract type (`(ArchitectureDependencyContract)contract`) still happens, exactly as it does today inside each handler's `Execute` method; it just lives in the lambda body instead of a class body.

**Alternative considered**: keep `IArchitectureContractHandler` as an interface but let `ArchitectureContractFamilyDescriptor` hold an *instance* of it (`IArchitectureContractHandler Handler`) rather than a bare delegate. Rejected — this still requires 24 classes, just referenced from the descriptor instead of from DI; it doesn't remove the boilerplate the issue is about.

### 2. Registry sourced from the static family registry, not DI

`ArchitectureContractHandlerRegistry` currently takes `IEnumerable<IArchitectureContractHandler>` and builds a `Dictionary<string, IArchitectureContractHandler>` keyed by `Family`, with a hardcoded `layer_template → layer` alias. The new registry builds `Dictionary<string, ArchitectureContractChecker>` by iterating `ArchitectureContractFamilyRegistry.All` and reading each descriptor's `Checker`. Because `layer_template`'s descriptor gets its own `Checker` (the same lambda body as `layer`'s — both cast to `ArchitectureLayerContract` and call `session.CheckLayerContract`), the alias special-case in the registry constructor is no longer needed; it falls out of the descriptor list itself.

This drops the constructor parameter entirely (or reduces it to none) — `ArchitectureContractHandlerRegistry` no longer needs anything injected because its source of truth is the static registry, matching how `ArchitectureContractCatalog.Build` already consumes `ArchitectureContractFamilyRegistry.All` directly rather than via DI.

**Alternative considered**: keep the `IEnumerable<...>` constructor for testability, but source the enumerable from the descriptors instead of DI. Rejected as unnecessary indirection — the descriptor list is already a single static, deterministic source (`internal static class ArchitectureContractFamilyRegistry`), and existing tests for the family registry (order, count, no-duplicates) already exercise it directly without DI.

### 3. Public API shape preserved

`IArchitectureContractHandlerRegistry.TryGetHandler(string family, out ...)` and `.Execute(string family, session, contract)` keep their names and call sites in `ArchitectureContractExecutor` untouched. `TryGetHandler`'s `out` parameter type changes from `IArchitectureContractHandler?` to the new checker delegate type (or the method is simplified to a existence check) — whichever keeps `ArchitectureContractExecutor` and test call sites compiling with the smallest diff. `ArchitectureHandlerResult` (with `FromViolations`/`FromCycles`) is untouched; it's still the correct return shape for "either violations or cycles."

### 4. `IArchitectureContractHandler` interface removed

Once no class implements it, keeping the interface around is dead surface. It's deleted along with `ArchitectureContractHandlers.cs`. `ArchitectureHandlerResult` moves (or stays, if convenient) in `Execution/Abstractions/IArchitectureContractHandler.cs` — that file is renamed/repurposed to hold the new `ArchitectureContractChecker` delegate and `ArchitectureHandlerResult` record instead.

## Risks / Trade-offs

- **[Risk]** `ArchitectureContractFamilyRegistry.All`'s file grows larger (each of the 25 entries now carries a lambda body, not just metadata) → **Mitigation**: the lambda bodies are one line each (same as the handler classes today), so total line count doesn't grow meaningfully; it consolidates rather than expands.
- **[Risk]** Existing tests construct `ArchitectureContractHandlerRegistry` from a hand-picked list of ~16 handler instances (`ArchitectureContractHandlerRegistryTests.CreateRegistry()`) → these won't compile once handler classes are deleted → **Mitigation**: rewrite `CreateRegistry()` to build from the descriptor path (parameterless constructor or `ArchitectureContractFamilyRegistry.All`); all downstream assertions (family lookups, behavior-matching checks) stay meaningful since they now exercise the real production wiring instead of a hand-curated subset.
- **[Risk]** `contract-handler-execution` spec's existing requirements explicitly describe DI-populated handlers and per-family `AddSingleton` registration as required behavior → these are now false → **Mitigation**: MODIFIED requirement deltas in this change rewrite them to describe the descriptor-sourced registry.

## Migration Plan

Pure internal refactor, no data migration, no config/CLI-visible change. Land behind the issue-211 branch, run `make fmt && make acceptance`, merge via PR. No rollback complexity beyond a normal revert — no persisted state changes shape.
