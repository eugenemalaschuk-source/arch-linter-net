## Why

`ArchLinterNet.Unity` has no Unity framework dependency. Its only production type is a convenience facade over the asmdef application service already implemented in Core, while the separate project creates an additional package, test assembly, solution entry, release step, architecture layer, and installation choice.

Issue #301 removes that artificial boundary. No public package listing was found that required a transition facade, so the redundant package is removed directly during the preview-stage product lifecycle.

## What Changes

- Move the `AsmdefValidator.Validate(...)` convenience API into `ArchLinterNet.Core.Asmdef` while preserving behavior and the public `contractPath` parameter name.
- Keep `ArchitectureEngine.ValidateAsmdef` and `AsmdefValidationService` as the single authoritative execution path.
- Move facade behavior and delegation tests into Core tests, including named-argument compatibility coverage.
- Remove the former production and test projects from the solution.
- Remove the standalone package from release packing and publication.
- Add pull-request package validation that runs `make pack` and requires exactly Core, CLI, and Testing artifacts.
- Update the self-architecture policy and all active documentation, including the Core architecture blueprint.

## Capabilities

### Modified Capabilities

- `asmdef-validation-service`: the convenience facade is now a Core API.
- `manual-nuget-release`: official releases and pull-request package validation produce Core, CLI, and Testing only.
- `self-architecture-policy`: asmdef validation is governed as a Core namespace capability, not a separate host assembly.

## Impact

- Added the Core facade, matching tests, and package-validation workflow.
- Removed the redundant production and test projects.
- Updated the solution, release workflow, make targets, self-policy, installation docs, agent guidance, active architecture blueprint, and active specifications.
- Consumers move namespaces, while method signatures, public parameter names, and behavior remain unchanged.
