## Why

The CEL (Common Expression Language) engine will eventually power rule evaluation in ArchLinterNet. Before any language behavior can be implemented, the physical assembly boundary must exist: a governed, independently buildable `ArchLinterNet.CEL` package that Core can depend on without coupling CLI, Testing, or external YAML/Roslyn infrastructure into the engine.

## What Changes

- Add `src/ArchLinterNet.CEL/ArchLinterNet.CEL.csproj` — minimal placeholder assembly, packable, no dependencies on other ArchLinterNet projects
- Add `tests/ArchLinterNet.CEL.Tests/ArchLinterNet.CEL.Tests.csproj` — NUnit test project, references CEL only
- Register both projects in `ArchLinterNet.slnx`
- Add `ProjectReference` from `ArchLinterNet.Core` to `ArchLinterNet.CEL`
- Extend `architecture/dependencies.arch.yml` with a `cel` layer, `ArchLinterNet.CEL` in `target_assemblies` and `assembly_search_paths`, namespace-coverage root, and reverse-dependency prohibition contracts
- Add architecture tests in `ArchLinterNet.Core.Tests` that verify the boundary and prove that a reverse dependency fails the policy (placed in `Core.Tests` rather than `CEL.Tests` because the tests need `ArchitectureValidationService` from Core, which `CEL.Tests` must not reference)
- Validate that `rtk make pack` produces both packages and that Core's `.nuspec` declares CEL as a dependency

## Capabilities

### New Capabilities

- `cel-project-boundary`: CEL assembly and test project are governed by the architecture policy, independently buildable and packable, with Core depending on CEL (not the reverse)

### Modified Capabilities

- `self-architecture-policy`: Repository package and assembly inventory scenario must include `ArchLinterNet.CEL` as a 4th production package

## Impact

- `ArchLinterNet.slnx` — two new project entries
- `src/ArchLinterNet.Core/ArchLinterNet.Core.csproj` — gains a `ProjectReference` to CEL
- `architecture/dependencies.arch.yml` — new layer, contracts, coverage entries
- `tests/ArchLinterNet.Core.Tests/SelfArchitecturePolicyTests.cs` — existing test continues to pass (CEL assembly now in policy scope)
- `nupkg/` — two packages emitted; Core `.nuspec` declares CEL dependency
