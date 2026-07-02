# infrastructure-seams Specification

## Purpose
Defines the replaceable infrastructure seams (file system, environment, assembly loading/probing, Roslyn compilation) that give behavior-affecting IO/runtime access in Core a fakeable boundary, with real default implementations registered in the composition root, while every existing public call site keeps its original signature and behavior.

## Requirements
### Requirement: File system access is behind a replaceable seam
`ArchLinterNet.Core` SHALL expose `IArchitectureFileSystem` (file existence, read-all-text, directory existence, file enumeration by pattern/search-option, last-write-time-UTC, current directory) with a default real implementation registered as a singleton in `ServiceCollectionExtensions.AddArchLinterNetCore()`. `ArchitectureContractLoader`, `ArchitectureBaselineLoader`, `ArchitectureProjectDiscovery`, `ArchitectureSourceScanner`, and `ArchitectureAsmdefScanner` SHALL accept an optional `IArchitectureFileSystem` parameter defaulting to the real singleton, so their file system access is replaceable without changing any existing call site.

#### Scenario: Existing static call sites are unaffected
- **WHEN** existing production or test code calls `ArchitectureContractLoader.LoadFromPath(path)`, `ArchitectureBaselineLoader.LoadFromPath(path)`, `ArchitectureProjectDiscovery.ResolveFromDocument(document, root)`, `ArchitectureSourceScanner.FindMethodBodyViolations(...)`, or `ArchitectureAsmdefScanner`'s scanning entry point without supplying a file system argument
- **THEN** each behaves exactly as before this change, reading from the real file system

#### Scenario: A fake file system replaces real IO for a DI-composed service
- **WHEN** a test constructs `ArchitectureProjectDiscoveryService` (or another DI-registered wrapper that depends on `IArchitectureFileSystem`) with a fake `IArchitectureFileSystem` implementation backed by an in-memory map instead of the real disk
- **THEN** the service's discovery/loading logic runs correctly using only the fake's data, without touching the real file system

### Requirement: Environment and runtime-base-directory access is behind a replaceable seam
`ArchLinterNet.Core` SHALL expose `IArchitectureEnvironment` (environment variable lookup, base directory) with a default real implementation registered as a singleton in `ServiceCollectionExtensions.AddArchLinterNetCore()`. `ArchitectureAssemblyResolver` and `ArchitectureRepositoryRootLocator` SHALL accept an optional `IArchitectureEnvironment` parameter defaulting to the real singleton.

#### Scenario: Existing static call sites are unaffected
- **WHEN** existing production or test code calls `ArchitectureAssemblyResolver.ResolveFromDocument(...)` or `ArchitectureRepositoryRootLocator.Resolve()`/`ResolveFrom(...)` without supplying an environment argument
- **THEN** each behaves exactly as before this change, reading the real environment variables and base directory

#### Scenario: A fake environment replaces real environment access in a test
- **WHEN** a test supplies a fake `IArchitectureEnvironment` returning a fixed environment-variable value or base directory
- **THEN** the consuming method uses the fake's values instead of the process's real environment

### Requirement: Assembly loading and probing is behind a replaceable seam
`ArchLinterNet.Core` SHALL expose `IArchitectureAssemblyLoader` (`Load(AssemblyName)`, `LoadFrom(string path)`) with a default real implementation registered as a singleton in `ServiceCollectionExtensions.AddArchLinterNetCore()`. `ArchitectureAssemblyResolver` SHALL accept an optional `IArchitectureAssemblyLoader` parameter defaulting to the real singleton, used wherever it currently calls `Assembly.Load`/`Assembly.LoadFrom` directly.

#### Scenario: Existing static call sites are unaffected
- **WHEN** existing production or test code calls `ArchitectureAssemblyResolver.ResolveFromDocument(...)` without supplying an assembly-loader argument
- **THEN** it behaves exactly as before this change, loading real assemblies from disk

#### Scenario: A fake assembly loader replaces real assembly loading in a test
- **WHEN** a test supplies a fake `IArchitectureAssemblyLoader` that returns an in-memory/already-loaded `Assembly` instead of loading from disk
- **THEN** `ArchitectureAssemblyResolver` uses the fake's returned assembly without invoking `Assembly.Load`/`Assembly.LoadFrom`

### Requirement: Roslyn compilation creation is behind a replaceable seam
`ArchLinterNet.Core` SHALL expose `IRoslynCompilationFactory` (creating a `CSharpCompilation` from source files and resolving its metadata references, including trusted-platform-assembly and loaded-assembly enumeration) with a default real implementation registered as a singleton in `ServiceCollectionExtensions.AddArchLinterNetCore()`. `ArchitectureSourceScanner` SHALL accept an optional `IRoslynCompilationFactory` parameter defaulting to the real singleton, used wherever it currently calls `CSharpCompilation.Create`/`MetadataReference.CreateFromFile` directly.

#### Scenario: Existing static call sites are unaffected
- **WHEN** existing production or test code calls `ArchitectureSourceScanner.FindMethodBodyViolations(...)` without supplying a compilation-factory argument
- **THEN** it behaves exactly as before this change, building a real Roslyn compilation from the discovered source files

#### Scenario: A fake compilation factory replaces real Roslyn compilation in a test
- **WHEN** a test supplies a fake `IRoslynCompilationFactory` that returns a pre-built `CSharpCompilation` instead of parsing real files and resolving real metadata references
- **THEN** `ArchitectureSourceScanner` performs its method-body-violation analysis against the fake's compilation without invoking the real Roslyn compilation pipeline

### Requirement: DI-registered wrapper services depend on infrastructure seams through their constructors
`ArchitecturePolicyDocumentLoader`, `ArchitectureBaselineLoadingService`, `ArchitectureProjectDiscoveryService`, `ArchitectureAssemblyResolutionService`, and `ArchitectureRepositoryRootResolver` SHALL receive the relevant seam interface(s) (`IArchitectureFileSystem`, `IArchitectureEnvironment`, `IArchitectureAssemblyLoader`, `IRoslynCompilationFactory`) via constructor injection and pass them explicitly into the static classes they wrap, rather than letting those static classes silently default to the real singleton.

#### Scenario: A DI-composed engine still uses real infrastructure by default
- **WHEN** an `ArchitectureEngine` is built via `ArchitectureEngineBuilder().AddArchLinterNetCore().Build()`
- **THEN** the resolved wrapper services use the container-registered real seam implementations, producing the same observable `ValidationOutcome`/`BaselineGenerationOutcome` as before this change

#### Scenario: A wrapper service's IO is faked without faking the whole service
- **WHEN** a test constructs one of the wrapper services directly with a fake seam implementation (e.g. `ArchitectureProjectDiscoveryService` with a fake `IArchitectureFileSystem`) instead of a fake of the wrapper service itself
- **THEN** the wrapper service's own discovery/loading logic executes against the fake seam, proving the seam — not just the wrapper's outer interface — is independently replaceable

