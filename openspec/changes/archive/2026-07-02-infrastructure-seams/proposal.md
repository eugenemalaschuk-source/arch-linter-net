## Why

Even after #136/#138 introduced focused, constructor-injected setup/session services, the heavy IO and runtime-integration points behind them — file system access, YAML deserialization, source/project discovery, Roslyn compilation, assembly loading/probing, environment variable access, and build-output staleness timestamps — are still direct static calls (`File.*`, `Directory.*`, `Environment.GetEnvironmentVariable`, `CSharpCompilation.Create`, `Assembly.Load*`) inside static classes with no interface. Where a DI-registered service wrapper already exists (`ArchitecturePolicyDocumentLoader`, `ArchitectureBaselineLoadingService`, `ArchitectureProjectDiscoveryService`, `ArchitectureAssemblyResolutionService`, `ArchitectureRepositoryRootResolver`), it just delegates straight into the static class beneath it, so the interface gives no real leverage over the IO. Issue #139 asks for replaceable seams at these boundaries so behavior that depends on external runtime state can be faked in unit tests instead of requiring real files, a real Roslyn compilation, or a real assembly load.

## What Changes

- Add `IArchitectureFileSystem` (file exists/read, directory exists/enumerate, last-write-time) with a real default implementation, registered as a DI singleton.
- Add `IArchitectureEnvironment` (environment variable lookup, base directory) with a real default implementation, registered as a DI singleton.
- Add `IArchitectureAssemblyLoader` (`Load(AssemblyName)`, `LoadFrom(path)`) with a real default implementation, registered as a DI singleton, used by `ArchitectureAssemblyResolver`.
- Add `IRoslynCompilationFactory` wrapping `CSharpCompilation.Create`, `MetadataReference.CreateFromFile`, and trusted-platform-assembly/`AppDomain` assembly enumeration, with a real default implementation, registered as a DI singleton, used by `ArchitectureSourceScanner`.
- Thread these seams as optional trailing parameters (defaulting to a shared real singleton instance) through the existing static classes that perform the IO: `ArchitectureContractLoader`, `ArchitectureBaselineLoader`, `ArchitectureProjectDiscovery`, `ArchitectureSourceScanner`, `ArchitectureAsmdefScanner`, `ArchitectureAssemblyResolver`, `ArchitectureRepositoryRootLocator`. Every existing call site (production and ~30 test files) keeps compiling and behaving identically because the default resolves to the real implementation.
- Update the DI-registered wrapper services (`ArchitecturePolicyDocumentLoader`, `ArchitectureBaselineLoadingService`, `ArchitectureProjectDiscoveryService`, `ArchitectureAssemblyResolutionService`, `ArchitectureRepositoryRootResolver`) to take the relevant seam interface(s) via constructor injection and pass them explicitly into the static calls, so container-composed code goes through the seam and it can be faked in tests without faking the whole service.
- Register the four new seam interfaces' default implementations in `ServiceCollectionExtensions.AddArchLinterNetCore()`.
- Add a test that drives `ArchitectureProjectDiscoveryService` (solution parsing, project file discovery, stale build-output timestamp comparison — the most integration-heavy remaining path) through a fake `IArchitectureFileSystem`, proving discovery logic runs correctly without touching the real disk.

## Capabilities

### New Capabilities
- `infrastructure-seams`: replaceable seams for file system, environment, assembly loading, and Roslyn compilation access, with real default implementations registered in the composition root and threaded through the existing loader/discovery/scanner/resolver static classes without changing their observable behavior.

### Modified Capabilities
(none — no observable/spec-level behavior changes to existing capabilities; `runner-setup-services`, `yaml-contract-loading`, `project-discovery`, and `assembly-resolution` keep their documented behavior, they just gain injectable IO underneath)

## Impact

- `src/ArchLinterNet.Core/IO/` (new folder): `IArchitectureFileSystem`, `IArchitectureEnvironment`, `IArchitectureAssemblyLoader`, `IRoslynCompilationFactory` and their real implementations.
- `src/ArchLinterNet.Core/Contracts/ArchitectureContractLoader.cs`, `ArchitectureBaselineLoader.cs`, `ArchitecturePolicyDocumentLoader.cs`, `ArchitectureBaselineLoadingService.cs`.
- `src/ArchLinterNet.Core/Discovery/ArchitectureProjectDiscovery.cs`, `ArchitectureProjectDiscoveryService.cs`.
- `src/ArchLinterNet.Core/Scanning/ArchitectureSourceScanner.cs`, `ArchitectureAsmdefScanner.cs`.
- `src/ArchLinterNet.Core/Execution/ArchitectureAssemblyResolver.cs`, `ArchitectureAssemblyResolutionService.cs`.
- `src/ArchLinterNet.Core/Resolution/ArchitectureRepositoryRootLocator.cs`, `ArchitectureRepositoryRootResolver.cs`.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs` (register the four new seam interfaces).
- New/extended tests under `tests/ArchLinterNet.Core.Tests/` for the new seams and the fake-file-system-driven project discovery test.
