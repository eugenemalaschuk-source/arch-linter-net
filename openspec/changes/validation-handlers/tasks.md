## 1. New contract handlers

- [ ] 1.1 Add `AllowOnlyContractHandler` (`Family = "allow_only"`) to `src/ArchLinterNet.Core/Execution/ArchitectureContractHandlers.cs`, delegating to `runner.CheckAllowOnlyContract()`.
- [ ] 1.2 Add `MethodBodyContractHandler` (`Family = "method_body"`), delegating to `runner.CheckMethodBodyContract()`.
- [ ] 1.3 Add `AsmdefContractHandler` (`Family = "asmdef"`), delegating to `runner.CheckAsmdefContract()`.
- [ ] 1.4 Add `IndependenceContractHandler` (`Family = "independence"`), delegating to `runner.CheckIndependenceContract()`.
- [ ] 1.5 Add `ProtectedContractHandler` (`Family = "protected"`), delegating to `runner.CheckProtectedContract()`.
- [ ] 1.6 Add `ExternalContractHandler` (`Family = "external"`), delegating to `runner.CheckExternalContract()`.
- [ ] 1.7 Add `AcyclicSiblingContractHandler` (`Family = "acyclic_sibling"`), delegating to `runner.CheckAcyclicSiblingContract()` and returning via `ArchitectureHandlerResult.FromCycles()` with the same contract-ID prefixing convention used by `CycleContractHandler`.

## 2. Registry as an instance service

- [ ] 2.1 Change `ArchitectureContractHandlerRegistry` to a constructor taking `IEnumerable<IArchitectureContractHandler>`, building the family-keyed dictionary from each handler's `Family`, and adding the existing `layer_template` → `LayerContractHandler` alias at construction time.
- [ ] 2.2 Remove `ArchitectureContractHandlerRegistry.CreateDefault()`.
- [ ] 2.3 Keep `TryGetHandler`/`Execute` signatures and `InvalidOperationException`-on-unknown-family behavior unchanged.

## 3. Composition root registrations

- [ ] 3.1 Register all 11 `IArchitectureContractHandler` implementations in `ServiceCollectionExtensions.AddArchLinterNetCore()` via `services.AddSingleton<IArchitectureContractHandler, TImplementation>()`.
- [ ] 3.2 Register `ArchitectureContractHandlerRegistry` as `services.AddSingleton<ArchitectureContractHandlerRegistry>()`, resolved from the registered handlers.

## 4. Executor dispatch

- [ ] 4.1 Find all call sites of `ArchitectureContractExecutor.Execute` (expected: `ArchitectureRunnerSetupService`/the validation application service pipeline) and thread an `ArchitectureContractHandlerRegistry` instance into it as a parameter, sourced from the composition root's service provider.
- [ ] 4.2 Replace the 7 direct `runner.CheckAllowOnlyContract()` / `CheckMethodBodyContract()` / `CheckAsmdefContract()` / `CheckIndependenceContract()` / `CheckProtectedContract()` / `CheckExternalContract()` / `CheckAcyclicSiblingContract()` calls in `ArchitectureContractExecutor.Execute()` with `registry.Execute(family, runner, contract)`, preserving the existing per-family iteration order and timing instrumentation.
- [ ] 4.3 Remove the now-unused `static readonly` default registry field from `ArchitectureContractExecutor` (the registry is passed in, not self-constructed).

## 5. Architecture governance

- [ ] 5.1 Confirm `architecture/dependencies.arch.yml` already restricts `Microsoft.Extensions.DependencyInjection` imports to `ArchLinterNet.Core.Composition` (added in the prior composition-root change) and that no new handler/registry code violates it.
- [ ] 5.2 Run `rtk make lint-architecture` and resolve any new findings.

## 6. Tests

- [ ] 6.1 Update `tests/ArchLinterNet.Core.Tests/ArchitectureContractHandlerRegistryTests.cs`'s `CreateDefault_RegistersHandlersForMigratedFamilies` test (or its replacement) to assert all 12 family keys (including `layer_template`) resolve via the new constructor-based registry.
- [ ] 6.2 Add equivalence tests for at least the `allow_only` and `acyclic_sibling` families (one violations-shaped, one cycles-shaped), following the existing `DependencyHandler_MatchesDirectRunnerCheck`/`CycleHandler_WithCycle_PrefixesContractIdOntoEachCycle` pattern: handler result via registry SHALL equal the direct `runner.CheckXxxContract()` call result.
- [ ] 6.3 Update or add an executor-level test (following `Executor_RoutesMigratedFamiliesThroughRegistry_MatchesDirectRunnerCalls`) asserting the executor's output for every family is unchanged after the dispatch-path migration.
- [ ] 6.4 Confirm existing strict/audit, baseline-candidate, unmatched-ignore, and coverage tests still pass unmodified.

## 7. Validation

- [ ] 7.1 Run `rtk make fmt`.
- [ ] 7.2 Run `rtk task acceptance:fresh` (or `rtk make acceptance` if no `task` CLI is available) and fix failures.
