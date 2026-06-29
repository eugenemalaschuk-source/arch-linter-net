## Context

`ArchitectureValidationService.Validate` and `ArchitectureBaselineService.Generate` are static methods that internally call `ArchitectureRunnerFactory` and `ArchitectureContractExecutor` (also static) to build and run an `ArchitectureContractRunner`. The CLI, `ArchitectureValidator`, and the Testing adapter all call these two static services directly — there is no seam at which collaborators could be swapped, and no place a future caller could register an alternate validation or baseline implementation.

Issue #134 asks for a composition root using `Microsoft.Extensions.DependencyInjection`, scoped to this seam only — not a rewrite of `ArchitectureRunnerFactory`, the scanners, or the contract execution pipeline, none of which need to change.

## Goals / Non-Goals

**Goals:**
- Give Core an explicit composition root (`ArchitectureEngineBuilder`) that wires the two existing application-service pipelines through `Microsoft.Extensions.DependencyInjection`.
- Make the validation and baseline pipelines addressable through typed interfaces (`IArchitectureValidationApplicationService`, `IArchitectureBaselineApplicationService`) so a caller building an `ArchitectureEngine` could register a replacement.
- Keep `IServiceCollection`/`ServiceProvider` confined to `ArchitectureEngineBuilder`/`ArchitectureEngine` — no other Core type, and nothing in `Execution`, `Scanning`, `Resolution`, `Discovery`, or `Contracts`, references the container.
- Preserve `ArchitectureValidationService.Validate`, `ArchitectureBaselineService.Generate`, `ArchitectureValidator`, and the Testing adapter exactly as-is from a caller's perspective.

**Non-Goals:**
- Extracting every Core service (scanners, runner, discovery, etc.) into DI registrations. Only the two application-service seams named in the issue are wired through the container.
- Changing CLI behavior, output, or exit codes.
- Changing YAML/contract behavior.
- Making ArchLinterNet a long-lived hosted application (no `IHost`, no background services).

## Decisions

**1. Move pipeline bodies into instance classes implementing the new interfaces; static services become thin delegating facades.**
`ArchitectureValidationService.Validate`'s body moves, verbatim, into a new `ArchitectureValidationApplicationService : IArchitectureValidationApplicationService` (analogous for baseline). The static `ArchitectureValidationService.Validate` becomes `DefaultEngine.Value.Validate(request, timing)`, where `DefaultEngine` is a `Lazy<ArchitectureEngine>` built once via `ArchitectureEngineBuilder`. This is the smallest change that gives the pipeline an interface seam while leaving every existing call site (CLI, `ArchitectureValidator`, Testing adapter) untouched — they keep calling the same static method.
- Alternative considered: keep the pipeline logic in the static class and have the instance service just forward to it. Rejected — it inverts the natural ownership (the static facade should be the thin wrapper, not the reusable implementation) and is the same pattern the issue calls out as wanting to avoid (calling something a "service" that is really still static logic underneath).

**2. `ArchitectureEngineBuilder` wraps a plain `IServiceCollection`/`ServiceProvider`, not `IHostBuilder`.**
The issue explicitly warns against turning this into a long-lived hosted application. `ArchitectureEngineBuilder` is a small sealed class holding an `IServiceCollection`, with an `AddArchLinterNetCore()` extension method that registers the two application services (`AddSingleton`, since both are stateless), and a `Build()` method that calls `serviceCollection.BuildServiceProvider()` and returns a new `ArchitectureEngine` wrapping the resulting `ServiceProvider`.

**3. `ArchitectureEngine` is a thin resolver-and-call facade, not a generic service locator.**
`ArchitectureEngine` does not expose `GetService<T>` or similar — only the two typed methods (`Validate`, `GenerateBaseline`) that resolve their respective interface internally and invoke it. This satisfies "do not inject `IServiceProvider` into runners/scanners/checkers/handlers" by construction: the `ServiceProvider` never leaves `ArchitectureEngine`, and nothing downstream of the application service (runner, scanners, etc.) takes a constructor dependency on it.

**4. New types live under `ArchLinterNet.Core.Composition`.**
This namespace is the only place that may reference `Microsoft.Extensions.DependencyInjection` types, mirroring the existing namespace-as-boundary convention (e.g. `Core.Scanning` is already protected by `architecture/dependencies.arch.yml`). The architecture policy will gain a contract restricting `Microsoft.Extensions.DependencyInjection` imports to `ArchLinterNet.Core.Composition`, enforced the same way `Core.Scanning` internals are protected today.

**5. `IArchitectureBaselineApplicationService`/`IArchitectureValidationApplicationService` take the existing request/outcome record types unchanged.**
No new DTOs — `ValidationRequest`, `ValidationOutcome`, `BaselineGenerationRequest`, `BaselineGenerationOutcome` already exist and fully describe the inputs/outputs. Introducing parallel DTOs at the interface boundary would be a speculative abstraction with no concrete need.

## Risks / Trade-offs

- [Risk] Moving pipeline logic out of the static class could subtly change behavior (e.g. closures over static state, timing). → Mitigation: move the method body verbatim into the instance class; the static method becomes a one-line delegation; existing tests for `ArchitectureValidationService`/`ArchitectureBaselineService`/`ArchitectureValidator` continue to run unchanged against the facade and catch regressions.
- [Risk] A second composition root or DI usage could creep into adapters (CLI) over time, recreating the "container APIs everywhere" problem the issue warns against. → Mitigation: enforce via the architecture contract that only `Core.Composition` may import `Microsoft.Extensions.DependencyInjection`; CLI/Testing/Unity are restricted to depending on Core's public surface only, which does not include container types.
- [Risk] `AddSingleton` registrations for stateless services could mask future per-request state needs. → Mitigation: out of scope for this issue; revisit registration lifetime if/when application services gain per-request state.

## Open Questions

- None — the issue's acceptance criteria fully scope this change to the validation/baseline seam, and the existing static-facade pattern (`ArchitectureValidator` over `ArchitectureValidationService`) is reused for `ArchitectureValidationService`/`ArchitectureBaselineService` over `ArchitectureEngine`.
