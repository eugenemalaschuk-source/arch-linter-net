## Why

Adding a new architecture contract family currently requires hand-editing `ArchitectureContractCatalog.Build`, which contains ~40 near-identical `AddGroup(...)` calls (one strict/audit pair per family, each hardcoding the family id, the strict YAML group name, and the audit YAML group name), plus a separate hardcoded exclusion list in `IsGroupResolvable` that determines which families are baseline-capable. This central, repetitive method is one of several god-file extension points (per [issue #208](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/208)) that unrelated feature work keeps touching, which hurts parallel delivery and violates open/closed. Introducing a descriptor abstraction lets each family's catalog metadata be defined once, in one place, as data — with the catalog reduced to iterating that data — so future family additions extend the registry instead of editing a shared method by hand.

## What Changes

- Add `ArchitectureContractFamilyDescriptor`, a record capturing per-family catalog metadata: family id, strict YAML group name, audit YAML group name, baseline-capability flag, an accessor for the family's strict contracts, an accessor for its audit contracts, an informational list of owned CLR contract types, and an unused placeholder validation-hook slot reserved for a future task.
- Add `ArchitectureContractFamilyRegistry`, exposing the ordered, complete list of descriptors for all 25 existing contract families, in the exact order they appear in `ArchitectureContractCatalog.Build` today.
- Refactor `ArchitectureContractCatalog.Build` to iterate `ArchitectureContractFamilyRegistry.All` instead of calling `AddGroup` by hand per family, and to derive baseline-capability from each descriptor instead of the hardcoded `IsGroupResolvable` name check.
- No behavior change: family discovery, dispatch order, baseline-capable groups, and all public `ArchitectureContractCatalog` members keep their existing outputs for every existing policy document.
- Non-goals (explicitly out of scope, per the issue): the YAML DTO model (`ArchitectureContractGroups`) stays as typed lists; contract handlers, DI registration, `ArchitecturePolicyDocumentLoader` validation logic, `ArchitectureAnalysisSession` check methods, CLI behavior, diagnostics, and exit codes are unchanged; no runtime plugin loading is introduced.

## Capabilities

### New Capabilities
- `contract-family-registry`: the descriptor-driven registry that defines, per contract family, its YAML group names, dispatch order, baseline capability, and contract accessors — the extension point `ArchitectureContractCatalog` builds from instead of hand-written per-family wiring.

### Modified Capabilities
(none — no existing capability's observable requirements change; `ArchitectureContractCatalog`'s current behavior, as pinned by its existing tests, is preserved exactly)

## Impact

- `src/ArchLinterNet.Core/Execution/ArchitectureContractCatalog.cs`: `Build` refactored to loop over the registry; `IsGroupResolvable` replaced by a descriptor lookup.
- New file(s) under `src/ArchLinterNet.Core/Execution/`: `ArchitectureContractFamilyDescriptor.cs` and `ArchitectureContractFamilyRegistry.cs`.
- `tests/ArchLinterNet.Core.Tests/ArchitectureContractCatalogTests.cs`: unchanged assertions must continue to pass unmodified.
- New tests proving the registry's order has no duplicate family ids and matches the historical dispatch order.
- No changes to `ArchitectureContractModels.cs`, `ArchitectureContractHandlers.cs`, `ArchitectureContractHandlerRegistry.cs`, `ServiceCollectionExtensions.cs`, `ArchitecturePolicyDocumentLoader.cs`, `ArchitectureAnalysisSession.*`, or diagnostics mapping.
