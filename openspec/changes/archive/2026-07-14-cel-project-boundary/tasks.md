## 1. CEL Source Project

- [x] 1.1 Create `src/ArchLinterNet.CEL/` directory and `ArchLinterNet.CEL.csproj` with net10.0, PackageId, Description, no forbidden PackageReferences, and `InternalsVisibleTo` for CEL.Tests only
- [x] 1.2 Add a minimal placeholder class (e.g., `CelEngine.cs`) in namespace `ArchLinterNet.CEL` to make the assembly non-empty

## 2. CEL Test Project

- [x] 2.1 Create `tests/ArchLinterNet.CEL.Tests/` directory and `ArchLinterNet.CEL.Tests.csproj` with net10.0, IsPackable=false, NUnit stack packages, and a single ProjectReference to CEL
- [x] 2.2 Add a smoke test class (`CelEngineSmokeTests.cs`) with one passing test confirming `CelEngine` instantiates

## 3. Solution Registration

- [x] 3.1 Add `src/ArchLinterNet.CEL/ArchLinterNet.CEL.csproj` to `ArchLinterNet.slnx` under `/src/`
- [x] 3.2 Add `tests/ArchLinterNet.CEL.Tests/ArchLinterNet.CEL.Tests.csproj` to `ArchLinterNet.slnx` under `/tests/`

## 4. Core → CEL Dependency

- [x] 4.1 Add a `ProjectReference` to `ArchLinterNet.CEL.csproj` in `src/ArchLinterNet.Core/ArchLinterNet.Core.csproj`

## 5. Architecture Policy

- [x] 5.1 Add `cel` layer (`namespace: ArchLinterNet.CEL`) to the `layers:` section of `architecture/dependencies.arch.yml`
- [x] 5.2 Add `ArchLinterNet.CEL` to `analysis.target_assemblies`
- [x] 5.3 Add `src/ArchLinterNet.CEL/bin/Debug/net10.0` to `analysis.assembly_search_paths`
- [x] 5.4 Add `namespace: ArchLinterNet.CEL` root to the `namespace-coverage` contract's `roots:` list
- [x] 5.5 Add strict contract `cel-must-not-depend-on-core` (source: cel, forbidden: [core])
- [x] 5.6 Add strict contract `cel-must-not-depend-on-cli` (source: cel, forbidden: [cli])
- [x] 5.7 Add strict contract `cel-must-not-depend-on-testing` (source: cel, forbidden: [testing])

## 6. Architecture Tests

- [x] 6.1 Add `CelBoundaryArchitectureTests.cs` to `tests/ArchLinterNet.Core.Tests/` (not `CEL.Tests` — CEL.Tests must reference only CEL; Core.Tests already references both Core and Testing, making it the correct host for a test that needs `ArchitectureValidationService` and a real assembly that depends on Core). Two tests: one proving `cel-must-not-depend-on-core` fires on a reverse dependency, one proving it is silent when the CEL layer has no forbidden reference.

## 7. Validation

- [x] 7.1 Run `rtk make restore` to pull new project into the restore graph
- [x] 7.2 Run `rtk make fmt` and inspect formatting changes
- [x] 7.3 Run `rtk make acceptance` and confirm all tests pass including self-policy
- [x] 7.4 Run `rtk make pack` and inspect `nupkg/ArchLinterNet.Core.*.nupkg` `.nuspec` to confirm `ArchLinterNet.CEL` appears as a dependency
- [x] 7.5 Run focused CEL tests: `rtk dotnet test tests/ArchLinterNet.CEL.Tests --no-restore`

## 8. Spec Synchronization and Archive

- [x] 8.1 Compare implementation against specs and update delta files if behavior diverged (architecture test location updated to `Core.Tests`)
- [x] 8.2 Run `openspec validate --all` to confirm all specs pass (89/89)
- [x] 8.3 Run `openspec archive cel-project-boundary` to merge deltas into main specs
- [x] 8.4 Run `openspec validate --all` again after archive (89/89)
