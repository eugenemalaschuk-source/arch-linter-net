## 1. Inventory

- [x] 1.1 Write `docs/internal/static-class-inventory.md` classifying all 35 production static classes that existed in `src/` before this change into: pure helper/mapper, extension container, constants holder, compatibility facade, or production service/orchestrator (with the 14 not-yet-converted category-(e) classes flagged as follow-up candidates and rationale for #142).

## 2. Convert ArchitectureContractExecutor

- [x] 2.1 Add `IArchitectureContractExecutor` interface (single `Execute` method, same signature/return type as the current static method) in `src/ArchLinterNet.Core/Execution/ArchitectureContractExecutor.cs`.
- [x] 2.2 Change `ArchitectureContractExecutor` from `internal static class` to `internal sealed class : IArchitectureContractExecutor` with an instance `Execute` method (same body).
- [x] 2.3 Register `IArchitectureContractExecutor` → `ArchitectureContractExecutor` as `AddSingleton` in `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`.
- [x] 2.4 Update `ArchitectureValidationApplicationService` to take `IArchitectureContractExecutor` as a primary-constructor parameter and call `executor.Execute(...)` instead of the static method.
- [x] 2.5 Update `ArchitectureBaselineApplicationService` the same way.
- [x] 2.6 Update the 4 static call sites in `tests/ArchLinterNet.Core.Tests/ArchitectureContractHandlerRegistryTests.cs` to construct `new ArchitectureContractExecutor()` and call the instance method.
- [x] 2.7 Extract the result record as a standalone top-level `public sealed record ArchitectureContractExecutionResult` (instead of a type nested inside `ArchitectureContractExecutor`), so the public `IArchitectureContractExecutor` interface doesn't reference a member of its internal implementation type.

## 3. Tests

- [x] 3.1 Add a focused unit test proving `ArchitectureContractExecutor` works correctly as an instance service (construct directly, run against a minimal composed session/handler-registry graph, assert dispatch/result shape).
- [x] 3.2 Run the full test suite and confirm no other call sites were missed and behavior is unchanged.

## 4. Validation

- [x] 4.1 Run `make fmt`.
- [x] 4.2 Run `make acceptance` (this repo's lint + test target — no Taskfile/`task acceptance:fresh` exists here) and fix any failures.
