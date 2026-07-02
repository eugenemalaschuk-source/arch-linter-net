## Why

Issue #154 (part of the #132 architecture-health story) asks that Core's production static classes be normalized: pure helpers/extension methods/constants/compatibility facades may stay static, but services, orchestrators, scanners, resolvers, and finders that own behavior, state, or collaborators should be instance-based and composition-root managed. The issue explicitly calls out `ArchitectureContractExecutor` as an example of execution orchestration that should not remain static. No repo-wide inventory of static classes currently exists, so it's not possible to tell which statics are intentional design choices versus unaddressed debt, and #142's self-policy guardrails have nothing to encode yet.

## What Changes

- Add a static-class inventory document (`docs/internal/static-class-inventory.md`) classifying every production `static class` under `src/` (34 total) into: pure helper/deterministic mapper, extension method container, constants/options holder, compatibility facade, or production service/orchestrator needing DI conversion.
- Convert `ArchitectureContractExecutor` (`internal static class` in `src/ArchLinterNet.Core/Execution/ArchitectureContractExecutor.cs`) — the orchestrator named directly in issue #154 — into an instance-based service behind a new `IArchitectureContractExecutor` interface, registered as a DI singleton in `ServiceCollectionExtensions.AddArchLinterNetCore()`.
- Inject `IArchitectureContractExecutor` into `ArchitectureValidationApplicationService` and `ArchitectureBaselineApplicationService` (both already constructor-DI classes) in place of the static `ArchitectureContractExecutor.Execute(...)` call.
- Update the 4 direct static call sites in `tests/ArchLinterNet.Core.Tests/ArchitectureContractHandlerRegistryTests.cs` to construct `new ArchitectureContractExecutor()` instead of calling the static method.
- Add a focused unit test exercising `ArchitectureContractExecutor` as an instance service with a minimal composed session/handler-registry graph.
- Document the remaining 13 category-(e) static classes (scanners, parsers, loaders, resolvers not yet converted) as tracked follow-up candidates with rationale, for #142 to encode as guardrails.

This is the first of several follow-up PRs for #154, consistent with the issue's own guidance that the work "may be split into smaller PRs by module." Converting all 14 identified production-service statics in one PR was assessed and rejected as too large/risky for one review; this PR establishes the inventory (an issue acceptance criterion in itself) and converts the one class the issue names explicitly, proving the pattern for later follow-ups.

## Capabilities

### New Capabilities
- `static-production-service-inventory`: a maintained classification of every production static class in `src/`, and the requirement that any class classified as a production service/orchestrator either be instance-based and DI-registered, or be documented as an explicit exception.

### Modified Capabilities
(none — `contract-handler-execution`'s dispatch behavior and public request/response shapes are unchanged; only `ArchitectureContractExecutor`'s ownership model moves from static to instance)

## Impact

- New: `docs/internal/static-class-inventory.md`.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractExecutor.cs` (static class → `IArchitectureContractExecutor` + `ArchitectureContractExecutor` instance class).
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs` (register `IArchitectureContractExecutor`).
- `src/ArchLinterNet.Core/Validation/ArchitectureValidationApplicationService.cs`, `ArchitectureBaselineApplicationService.cs` (constructor injection of the new service instead of static call).
- `tests/ArchLinterNet.Core.Tests/ArchitectureContractHandlerRegistryTests.cs` (4 call sites updated to instantiate directly) plus a new focused unit test file/section for the converted service.
