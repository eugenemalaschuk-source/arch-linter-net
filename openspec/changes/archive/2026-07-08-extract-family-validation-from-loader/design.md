## Context

`ArchitecturePolicyDocumentLoader` (`src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs`, 640 lines, plus a 315-line `CoverageValidation.cs` partial) deserializes `dependencies.arch.yml` and then runs ~17 validation steps against the parsed `ArchitectureContractDocument`, in a fixed sequence, before returning it. Two of those steps are genuinely cross-family (`ValidateDuplicateIds`, `ValidateLayerNamespaces`); the rest belong to one specific contract family each (assembly/package dependency & allow-only, project metadata, type placement, public API surface, attribute usage, inheritance, interface implementation, composition, acyclic sibling, and coverage's five scopes). Every new family has meant adding another private method and another call in `Load`.

Issue #208 added `ArchitectureContractFamilyDescriptor.AdditionalValidation` (`Action<ArchitectureContractDocument>?`) in `Execution/ArchitectureContractFamilyRegistry.cs`, with a design note that wiring it into `Load` was deliberately deferred to a later task. That later task is #210 (this change).

Investigation surfaced a hard constraint that wasn't visible at #208 time: `docs/internal/core-architecture-blueprint.md` states "`Contracts` depends on nothing else in Core" as a hard rule, and both `Execution/LayerTemplateExpander.cs` and `Reporting/ArchitectureSarifFormatter.cs` call `ArchitecturePolicyDocumentLoader.NormalizeToContractId` directly — so the loader cannot be relocated out of `Contracts` either. `Load` therefore cannot reach into `Execution.ArchitectureContractFamilyRegistry` to read `AdditionalValidation` without inverting that dependency direction. This design does not attempt that wiring.

Order matters: today's exceptions are thrown eagerly, first-match-wins, so a policy document invalid in two ways always fails with the first validator's message in the current call sequence. Several existing tests assert on specific message text, so both order and wording must be preserved exactly.

## Goals / Non-Goals

**Goals:**
- Give every contract family its own validator class, independently testable and independently extensible, without touching `ArchitecturePolicyDocumentLoader.cs` for new rules within an existing family.
- Preserve the exact validation order, exception types, and message text of the current implementation.
- Keep `Load`'s public signature and `IArchitecturePolicyDocumentLoader` contract unchanged.
- Stay entirely within `Contracts/` — no new dependency from `Contracts` onto `Execution` or any other module.

**Non-Goals:**
- Wiring `ArchitectureContractFamilyDescriptor.AdditionalValidation` into `Load` (architecturally infeasible per the constraint above; left unused as-is).
- Changing the YAML schema or adding new validation semantics.
- Rewriting contract execution/checking (`ArchitectureContractRunner`, `ArchitectureAnalysisSession`, etc.).
- Introducing DI/constructor injection for validators — the pipeline is a local, static, ordered list, consistent with `ArchitectureContractFamilyRegistry.All`'s existing pattern and requiring no changes to `ServiceCollectionExtensions.cs` or any test that does `new ArchitecturePolicyDocumentLoader()`.

## Decisions

### 1. `IArchitecturePolicyDocumentValidator` lives in `Contracts/`, not `Execution/`
All 15 extracted methods operate purely on the already-deserialized `ArchitectureContractDocument` (confirmed: none reference `policyPath` or raw YAML nodes). There is no runtime/execution state involved, so there's no architectural reason for these classes to live in `Execution/` — keeping them in `Contracts/` (new `Contracts/Validators/` subfolder) satisfies the "pure schema/data models" character of that module and avoids any new cross-module dependency.

### 2. One class per family, `CoverageValidator` as a single class wrapping five scopes
Mirrors the granularity already used elsewhere in the codebase (`ArchitectureContractFamilyRegistry.All` has one descriptor per family; test files are already one-per-family). Coverage is the exception: its dispatcher (`ValidateCoverageNamespaces`) and four scope helpers are tightly coupled (shared dispatch on `contract.Scope`, and the `rule_input` scope depends on `CollectLayerBearingContractIds`, which itself scans ~19 other families' contract groups). Splitting coverage into 5 separate `IArchitecturePolicyDocumentValidator` entries would require re-deriving that dispatch order across pipeline entries; folding it into one `CoverageValidator.Validate` that internally reproduces today's dispatcher call is simpler and lower-risk. `CollectLayerBearingContractIds` stays a shared internal helper (moves alongside the validators, or stays accessible to `CoverageValidator`).

### 3. Ordered pipeline as a static list, not a `foreach(family in registry)` reuse of `ArchitectureContractFamilyRegistry`
The two registries serve different concerns (contract cataloguing vs. document validation) and the family call order in `Load` does **not** match `ArchitectureContractFamilyRegistry.All`'s order 1:1 today (e.g. `acyclic_sibling` and the generic `ValidateLayerNamespaces` are interleaved with coverage before the assembly/package block, whereas the registry's order groups differently). Introducing a second, `Contracts`-local ordered list (`ArchitecturePolicyDocumentValidatorPipeline.All` or similar) that exactly encodes today's `Load` call sequence is the lowest-risk way to guarantee zero behavior change, versus trying to force a shared ordering abstraction between two registries with different existing orders and different module homes.

Pipeline order (identical to current `Load` call order):
`ValidateDuplicateIds` → `AcyclicSiblingValidator` → `ValidateLayerNamespaces` → `CoverageValidator` → `AssemblyIndependenceValidator` → `AssemblyDependencyValidator` → `AssemblyAllowOnlyValidator` → `PackageDependencyValidator` → `PackageAllowOnlyValidator` → `ProjectMetadataValidator` → `TypePlacementValidator` → `PublicApiSurfaceValidator` → `AttributeUsageValidator` → `InheritanceValidator` → `InterfaceImplementationValidator` → `CompositionValidator`.

`AssignFallbackIds` stays a direct call in `Load` (it mutates the document rather than validating it, so it doesn't fit the `Validate`-only interface and must run before `ValidateDuplicateIds` regardless).

### 4. `AdditionalValidation` remains unused; documented as a deliberate deviation
Rather than silently leaving #208's design note unresolved, this change's proposal and PR description will state explicitly why `AdditionalValidation` is not wired in, so a future reader doesn't reopen the question without first reading this rationale. The `contract-family-registry` spec's existing "AdditionalValidation is never invoked" scenario remains true and is not modified — it was scoped to "in this change" (#208) but nothing in #210 invokes it either, so no spec delta is needed there.

## Risks / Trade-offs

- **[Risk] Subtle reordering breaks message-first-wins behavior for documents invalid in multiple families** → Mitigation: pipeline order is a direct transcription of today's `Load` call sequence (see Decision 3); every family's extracted method body is a verbatim lift, not a rewrite; existing per-family test suites (16 files) are run unchanged as the regression gate.
- **[Risk] `CoverageValidator`'s internal coupling to `CollectLayerBearingContractIds` (which scans ~19 other families) makes it look like it still isn't "single-family owned"** → Accepted trade-off: this coupling is pre-existing behavior (not introduced by this change) and the issue's own non-goals rule out changing validation semantics; documented in code as a known coupling rather than hidden.
- **[Risk] Two parallel "family registries" (`ArchitectureContractFamilyRegistry` in Execution, new validator pipeline in Contracts) could drift or confuse future contributors adding a new family** → Mitigation: doc comment on the new pipeline file cross-referencing `ArchitectureContractFamilyRegistry` and explaining why they're separate (module boundary), plus an update to `docs/internal/core-architecture-blueprint.md`'s "adding a new contract family" checklist to mention both.

## Migration Plan

Not applicable in the deployment sense — this is an internal, non-breaking refactor of a single class's internals behind an unchanged public interface (`IArchitecturePolicyDocumentLoader`). No data migration, no API version bump. Rollback is a plain revert if `make acceptance` regresses.

## Open Questions

None outstanding — the dependency-direction question was the blocking unknown and is resolved by Decision 1.
