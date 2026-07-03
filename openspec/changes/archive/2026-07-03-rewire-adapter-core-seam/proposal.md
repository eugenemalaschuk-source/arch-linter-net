## Why

Issue #140 requires adapters to consume the composed Core application seam instead of continuing through legacy static facades or, for Unity, bypassing the seam entirely. The current `core-composition-root` spec still documents the static facades as sufficient for CLI, public API, and Testing callers, while the Unity adapter still owns policy loading, repository-root resolution, and asmdef scanning orchestration.

## What Changes

- Rewire the CLI validation and baseline commands to call an `ArchitectureEngine` built from the Core composition root instead of `ArchitectureValidationService` / `ArchitectureBaselineService` static facades.
- Rewire the public `ArchitectureValidator` compatibility API and `ArchLinterNet.Testing` validation builder to call the composed validation seam while preserving their existing public signatures.
- Add a narrow composed Core asmdef application service for Unity-facing `strict_asmdef` validation.
- Expose asmdef validation through `ArchitectureEngine` without exposing `IServiceProvider` or any container-specific API to adapters.
- Rewire `ArchLinterNet.Unity.AsmdefValidator` into a thin compatibility facade over the new asmdef service.
- Preserve CLI arguments, exit codes, public API result mapping, Testing adapter result mapping, and Unity asmdef validation behavior.

## Capabilities

### New Capabilities

- `asmdef-validation-service`: a narrow Core application service for Unity-facing asmdef-only validation.

### Modified Capabilities

- `core-composition-root`: the composed engine registers and exposes validation, baseline, and asmdef application services; static services remain compatibility facades but are no longer the required adapter path.

## Impact

- Affected code: `src/ArchLinterNet.Core/Composition/`, `src/ArchLinterNet.Core/Validation/`, `src/ArchLinterNet.Cli/`, `src/ArchLinterNet.Testing/`, `src/ArchLinterNet.Unity/`.
- Affected tests: Core engine/composition tests, Testing adapter tests, Unity asmdef adapter tests, self-architecture policy.
- No user-facing CLI, public API, Testing DSL, or Unity adapter signature changes.
