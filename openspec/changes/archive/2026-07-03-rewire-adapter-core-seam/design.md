## Context

`ArchitectureEngine`/`ArchitectureEngineBuilder` (from `core-composition-root`) already compose `IArchitectureValidationApplicationService` and `IArchitectureBaselineApplicationService` behind `AddArchLinterNetCore()`. CLI, `ArchitectureValidator`, and the Testing adapter still call the static `ArchitectureValidationService`/`ArchitectureBaselineService` facades (each of which lazily builds its own private `ArchitectureEngine` internally) rather than building/holding an engine themselves. `ArchLinterNet.Unity.AsmdefValidator` is the one adapter that never routed through any shared seam: it directly `new`s `ArchitecturePolicyDocumentLoader`, `ArchitectureRepositoryRootResolver`, and `ArchitectureAsmdefScanner`.

Per the #133 blueprint, Unity is deliberately *not* folded into the full validation seam (it only ever runs `StrictAsmdef` contracts and has no mode/baseline/condition-set/contract-ID concepts) — it needs its own narrow application service.

Constraint carried over from `core-composition-root`: only types under `ArchLinterNet.Core.Composition` may reference `Microsoft.Extensions.DependencyInjection` container types; no adapter (`cli`/`testing`/`unity`) may reference `IServiceProvider`/`IServiceCollection` directly. Adapters may only call `ArchitectureEngineBuilder`/`ArchitectureEngine` methods, never resolve arbitrary services.

## Goals / Non-Goals

**Goals:**
- CLI, `ArchitectureValidator`, and the Testing adapter call `ArchitectureEngine.Validate`/`GenerateBaseline` directly instead of the static facades.
- Add a narrow `IAsmdefValidationService`/`AsmdefValidationService` Core application service that owns policy loading, repository-root resolution, and `strict_asmdef` scanning for asmdef-only callers.
- `ArchitectureEngine` exposes `ValidateAsmdef` so Unity can consume the asmdef seam the same way CLI/API/Testing consume the validation seam — via the engine, never via `IServiceProvider`.
- `ArchLinterNet.Unity.AsmdefValidator` becomes a thin facade with no direct dependency on `Core.Contracts`/`Core.Resolution`/`Core.Scanning`.
- Preserve CLI args/exit codes, public API signatures, Testing adapter result shapes, and Unity's `bool Validate(...)`/`out violations` behavior exactly.

**Non-Goals:**
- Removing or changing the existing `ArchitectureValidationService`/`ArchitectureBaselineService` static facades — they stay as documented compatibility entry points for any consumer that isn't this repo's own adapters.
- Folding Unity into the full `ValidationRequest`/`ValidationOutcome` seam, or adding mode/baseline/condition-set/contract-ID semantics to asmdef-only validation.
- Changing `ArchitectureAnalysisSession.CheckAsmdefContract`'s direct `new ArchitectureAsmdefScanner()` call — that's internal full-pipeline execution code, not an adapter, and is out of this issue's scope.

## Decisions

- **Each adapter holds its own private, lazily-built `ArchitectureEngine`**, mirroring the pattern the static facades already use internally (`private static readonly Lazy<ArchitectureEngine> _engine = new(() => new ArchitectureEngineBuilder().AddArchLinterNetCore().Build())`), rather than sharing one process-wide engine across adapters. Alternative considered: a single shared static engine in a common location — rejected because it would need a new shared assembly/namespace all adapters reference, adding indirection with no behavioral benefit, and CLI already only needs one instance per process anyway.
- **`AsmdefValidationService` lives in a new `ArchLinterNet.Core.Asmdef` namespace/layer**, not folded into `ArchLinterNet.Core.Validation`. Alternative considered: adding it alongside `ArchitectureValidationApplicationService`/`ArchitectureBaselineApplicationService` in `Core.Validation` — rejected because `core_validation` denotes the full validation/baseline seam (`ValidationRequest`/`ValidationOutcome`), and the #133 blueprint explicitly treats asmdef as a separate, narrower concern; a distinct layer also lets the self-policy enforce a Unity-specific seam rule (`unity-must-use-asmdef-application-seam`) without also touching the full validation contracts CLI/API/Testing forbid.
- **`IArchitectureAsmdefScanner`/`ArchitectureAsmdefScanner` change from `internal` to `public`.** `AsmdefValidationService`'s constructor is `public` (matching the convention of every other DI-registered application service), and C# forbids a public constructor from taking a less-accessible parameter type (CS0051). Alternative considered: an internal constructor with an internal factory method — rejected because Microsoft.Extensions.DependencyInjection's default constructor-selection only considers public constructors, so `services.AddSingleton<IAsmdefValidationService, AsmdefValidationService>()` would fail to resolve at runtime (confirmed by reproducing the `InvalidOperationException: A suitable constructor ... could not be located` failure during implementation).
- **`unity-must-use-asmdef-application-seam` mirrors `cli-must-use-validation-application-seam` exactly** (same forbidden layer set: `core_execution`, `core_contracts`, `core_resolution`, `core_scanning`), keeping the two adapter-seam rules symmetric and easy to reason about together.

## Risks / Trade-offs

- [Making `ArchitectureAsmdefScanner` public slightly widens its public-API surface beyond what `core-scanning-internals-are-protected` originally intended] → The self-architecture policy's protected-namespace contract governs *namespace usage* (import edges), not CLR accessibility; no namespace outside `core`/`core_asmdef` imports `ArchLinterNet.Core.Scanning` after this change (Unity's direct references are removed), so the protection is preserved in the dimension the policy actually checks. Verified via `dotnet arch-linter-net --mode strict` and `--mode audit` passing with zero violations after the change.
- [Each adapter building its own `ArchitectureEngine`/`ServiceProvider` means CLI, `ArchitectureValidator`, and the Testing adapter each hold a separate container instance rather than sharing one] → Matches existing behavior (the static facades already do this — `ArchitectureValidationService` and `ArchitectureBaselineService` each build their own engine); no regression, and each adapter process only ever needs one engine for its lifetime.

## Migration Plan

Purely additive/internal rewiring; no data migration. Rollout is the normal PR/merge process. Rollback is reverting the PR — no persisted state changes.
