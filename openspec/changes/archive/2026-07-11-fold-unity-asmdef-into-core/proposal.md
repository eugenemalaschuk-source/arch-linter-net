## Why

`ArchLinterNet.Unity` contains no Unity runtime, editor, package, or assembly dependency. Its only production type is a convenience facade over the asmdef application service already implemented in `ArchLinterNet.Core`, while the separate project creates an additional NuGet artifact, test assembly, solution entry, release step, architecture layer, and installation choice.

Issue #301 removes that artificial packaging boundary. Verification performed while implementing the issue found no public `ArchLinterNet.Unity` package listing requiring a transition facade, so the redundant package is removed directly during the preview-stage product lifecycle.

## What Changes

- Move the `AsmdefValidator.Validate(...)` convenience API into `ArchLinterNet.Core.Asmdef` without changing its boolean/violation behavior.
- Keep `ArchitectureEngine.ValidateAsmdef` and `AsmdefValidationService` as the single authoritative execution path.
- Migrate facade behavior/delegation tests into `ArchLinterNet.Core.Tests`.
- Remove the `ArchLinterNet.Unity` production and test projects from the repository and solution.
- Remove the Unity package from release packing and publication.
- Simplify the self-architecture policy to the three real production assemblies: Core, CLI, and Testing.
- Update installation, agent, static-inventory, and active OpenSpec documentation.

## Capabilities

### Modified Capabilities

- `asmdef-validation-service`: the convenience facade is now a Core API rather than a Unity adapter API.
- `manual-nuget-release`: official releases pack Core, CLI, and Testing only.
- `self-architecture-policy`: asmdef validation is governed as a Core namespace capability, not a separate host assembly.

## Impact

- Added: `src/ArchLinterNet.Core/Asmdef/AsmdefValidator.cs` and matching Core tests.
- Removed: `src/ArchLinterNet.Unity/**` and `tests/ArchLinterNet.Unity.Tests/**`.
- Updated: solution, release workflow, make targets, self-policy, installation docs, agent guidance, and active specifications.
- Source compatibility changes for consumers of the unreleased/preview facade from `ArchLinterNet.Unity.AsmdefValidator` to `ArchLinterNet.Core.Asmdef.AsmdefValidator`; method signatures and behavior remain unchanged.
