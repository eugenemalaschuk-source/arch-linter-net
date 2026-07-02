## 1. File system seam

- [x] 1.1 Add `IArchitectureFileSystem` (FileExists, ReadAllText, DirectoryExists, EnumerateFiles(path, pattern, SearchOption), GetLastWriteTimeUtc, GetCurrentDirectory) and a real default implementation (e.g. `ArchitectureFileSystem` with a static `Real` singleton instance) under `src/ArchLinterNet.Core/IO/`.
- [x] 1.2 Thread `IArchitectureFileSystem? fileSystem = null` (defaulting to the real singleton) through `ArchitectureContractLoader.LoadFromPath`/`LoadFromRepositoryRoot`, `ArchitectureBaselineLoader.LoadFromPath`, `ArchitectureProjectDiscovery.ResolveFromDocument` (and its private helpers that call `File.Exists`/`Directory.Exists`/`Directory.EnumerateFiles`/`File.GetLastWriteTimeUtc`), `ArchitectureSourceScanner`'s file-reading helpers, and `ArchitectureAsmdefScanner`'s scanning entry point.
- [x] 1.3 Register the real `IArchitectureFileSystem` implementation as a singleton in `ServiceCollectionExtensions.AddArchLinterNetCore()`.

## 2. Environment seam

- [x] 2.1 Add `IArchitectureEnvironment` (GetEnvironmentVariable, BaseDirectory) and a real default implementation under `src/ArchLinterNet.Core/IO/`.
- [x] 2.2 Thread `IArchitectureEnvironment? environment = null` (defaulting to the real singleton) through `ArchitectureAssemblyResolver`'s environment-variable read and `ArchitectureRepositoryRootLocator.Resolve`/`ResolveFrom`.
- [x] 2.3 Register the real `IArchitectureEnvironment` implementation as a singleton in `ServiceCollectionExtensions.AddArchLinterNetCore()`.

## 3. Assembly loader seam

- [x] 3.1 Add `IArchitectureAssemblyLoader` (`Load(AssemblyName)`, `LoadFrom(string path)`) and a real default implementation under `src/ArchLinterNet.Core/IO/`.
- [x] 3.2 Thread `IArchitectureAssemblyLoader? assemblyLoader = null` (defaulting to the real singleton) through `ArchitectureAssemblyResolver`'s `Assembly.Load`/`Assembly.LoadFrom` call sites.
- [x] 3.3 Register the real `IArchitectureAssemblyLoader` implementation as a singleton in `ServiceCollectionExtensions.AddArchLinterNetCore()`.

## 4. Roslyn compilation factory seam

- [x] 4.1 Add `IRoslynCompilationFactory` (build a `CSharpCompilation` from source file paths plus preprocessor symbols, resolving metadata references from trusted-platform-assemblies and loaded assemblies) and a real default implementation under `src/ArchLinterNet.Core/IO/`, moving `ArchitectureSourceScanner.BuildCompilation`/`BuildMetadataReferences` logic into it.
- [x] 4.2 Thread `IRoslynCompilationFactory? compilationFactory = null` (defaulting to the real singleton) through `ArchitectureSourceScanner.FindMethodBodyViolations`.
- [x] 4.3 Register the real `IRoslynCompilationFactory` implementation as a singleton in `ServiceCollectionExtensions.AddArchLinterNetCore()`.

## 5. Wire seams into the DI-registered wrapper services

- [x] 5.1 Update `ArchitecturePolicyDocumentLoader` and `ArchitectureBaselineLoadingService` to take `IArchitectureFileSystem` via constructor and pass it into `ArchitectureContractLoader`/`ArchitectureBaselineLoader`.
- [x] 5.2 Update `ArchitectureProjectDiscoveryService` to take `IArchitectureFileSystem` via constructor and pass it into `ArchitectureProjectDiscovery.ResolveFromDocument`.
- [x] 5.3 Update `ArchitectureAssemblyResolutionService` to take `IArchitectureFileSystem`, `IArchitectureEnvironment`, and `IArchitectureAssemblyLoader` via constructor and pass them into `ArchitectureAssemblyResolver.ResolveFromDocument`.
- [x] 5.4 Update `ArchitectureRepositoryRootResolver` to take `IArchitectureFileSystem` and `IArchitectureEnvironment` via constructor and pass them into `ArchitectureRepositoryRootLocator.Resolve`/`ResolveFrom`.
- [x] 5.5 Confirm no public constructor signature used outside DI (i.e. anything tests construct directly with `new`) breaks — add the new constructor parameters as required (not optional) on these wrapper services, since they are only ever constructed via `ServiceCollectionExtensions` or the existing `ArchitectureRunnerSetupServiceFakeDependencyTests`-style fakes, and update that one test file's direct construction if needed.

## 6. Tests

- [x] 6.1 Add a fake `IArchitectureFileSystem` (in-memory) test double under `tests/ArchLinterNet.Core.Tests/`.
- [x] 6.2 Add a test driving `ArchitectureProjectDiscoveryService` (solution parsing, project discovery, stale build-output timestamp comparison) through the fake file system, proving it runs without touching the real disk, per the `infrastructure-seams` spec's "fake file system replaces real IO" scenario.
- [x] 6.3 Add a focused unit test per new seam (`IArchitectureEnvironment`, `IArchitectureAssemblyLoader`, `IRoslynCompilationFactory`) proving the consuming static method/service uses the fake instead of the real implementation.
- [x] 6.4 Run the full existing test suite unchanged and confirm every direct-static-call test (contract loader, baseline loader, project discovery, source scanner, asmdef scanner, assembly resolver, repository root locator tests) still passes without modification.

## 7. Validation and spec sync

- [x] 7.1 Run `make fmt`.
- [x] 7.2 Run `make acceptance` (lint + test); fix any failures.
- [x] 7.3 Confirm the `infrastructure-seams` delta spec accurately reflects the final seam interfaces and wiring.
- [x] 7.4 Run `openspec validate --all`.
- [x] 7.5 Run `openspec archive infrastructure-seams`.
