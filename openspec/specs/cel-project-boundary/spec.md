# cel-project-boundary Specification

## Purpose
TBD - created by archiving change cel-project-boundary. Update Purpose after archive.
## Requirements
### Requirement: CEL assembly is independently buildable and packable
`ArchLinterNet.CEL` SHALL build and produce a valid NuGet package in isolation. It SHALL NOT carry a `ProjectReference` to any other ArchLinterNet assembly (`Core`, `Cli`, or `Testing`). It SHALL NOT reference external packages that would pull in YAML loading, JSON schema, MSBuild evaluation, Roslyn compilation, or ArchLinterNet IO abstractions.

#### Scenario: CEL project builds without other ArchLinterNet projects
- **WHEN** `dotnet build src/ArchLinterNet.CEL/ArchLinterNet.CEL.csproj` is run in isolation
- **THEN** the build succeeds without references to `ArchLinterNet.Core`, `ArchLinterNet.Cli`, or `ArchLinterNet.Testing`

#### Scenario: CEL package is produced
- **WHEN** `dotnet pack` is run for the solution
- **THEN** a `nupkg/ArchLinterNet.CEL.*.nupkg` file is produced with no forbidden dependencies in its `.nuspec`

### Requirement: Core depends on CEL, not the reverse
`ArchLinterNet.Core` SHALL declare a direct `ProjectReference` to `ArchLinterNet.CEL`. `ArchLinterNet.CEL` SHALL NOT reference `ArchLinterNet.Core`, `ArchLinterNet.Cli`, or `ArchLinterNet.Testing` directly or transitively.

#### Scenario: Core project reference graph includes CEL
- **WHEN** `ArchLinterNet.Core.csproj` is inspected
- **THEN** it contains a `ProjectReference` entry pointing to `ArchLinterNet.CEL.csproj`

#### Scenario: Packed Core declares CEL as a dependency
- **WHEN** `nupkg/ArchLinterNet.Core.*.nupkg` is unpacked and its `.nuspec` inspected
- **THEN** it declares `ArchLinterNet.CEL` as a `<dependency>` rather than embedding its implementation

### Requirement: Architecture policy governs the CEL assembly
The repository's architecture contract (`architecture/dependencies.arch.yml`) SHALL declare a `cel` layer for namespace `ArchLinterNet.CEL`, SHALL include `ArchLinterNet.CEL` in `target_assemblies` and in the `assembly_search_paths`, and SHALL include `ArchLinterNet.CEL` as a root in the `namespace-coverage` contract.

#### Scenario: CEL layer is declared in the policy
- **WHEN** `architecture/dependencies.arch.yml` is loaded
- **THEN** it defines a layer named `cel` with `namespace: ArchLinterNet.CEL`
- **AND** `ArchLinterNet.CEL` appears in `target_assemblies`

#### Scenario: CEL assembly is covered by namespace-coverage
- **WHEN** the self-architecture policy runs in strict mode
- **THEN** the namespace-coverage contract includes `ArchLinterNet.CEL` as a root namespace
- **AND** the coverage contract does not report an uncovered namespace for the CEL assembly

### Requirement: Reverse-dependency contracts prohibit CEL from depending on product assemblies
The architecture policy SHALL declare strict contracts prohibiting `ArchLinterNet.CEL` from depending on `ArchLinterNet.Core`, `ArchLinterNet.Cli`, or `ArchLinterNet.Testing`.

#### Scenario: Reverse-dependency contract is declared for Core
- **WHEN** `architecture/dependencies.arch.yml` is loaded
- **THEN** it contains a contract with source `cel` that lists `core` in its `forbidden` set

#### Scenario: Reverse-dependency contract is declared for CLI and Testing
- **WHEN** `architecture/dependencies.arch.yml` is loaded
- **THEN** it contains contracts with source `cel` that list `cli` and `testing` in their `forbidden` sets

#### Scenario: A synthetic reverse dependency fails the policy
- **WHEN** an architecture test maps a real assembly that references `ArchLinterNet.Core` (e.g., `ArchLinterNet.Testing`) to the `cel` layer in a temporary policy
- **THEN** running the policy in strict mode produces a violation for the `cel-must-not-depend-on-core` contract
- **AND** a complementary test that maps `ArchLinterNet.Core` itself to the `cel` layer (no forbidden reference) produces no violations

### Requirement: CEL and Core test projects can reference their respective assemblies
`ArchLinterNet.CEL.Tests` SHALL reference `ArchLinterNet.CEL` only. It SHALL be present in the solution under the `/tests/` folder. `InternalsVisibleTo` in the CEL project MAY grant access to `ArchLinterNet.CEL.Tests` but SHALL NOT grant access to `ArchLinterNet.Core`.

#### Scenario: CEL.Tests project is registered in the solution
- **WHEN** `ArchLinterNet.slnx` is inspected
- **THEN** it lists both `src/ArchLinterNet.CEL/ArchLinterNet.CEL.csproj` and `tests/ArchLinterNet.CEL.Tests/ArchLinterNet.CEL.Tests.csproj`

#### Scenario: Focused CEL test execution succeeds
- **WHEN** `dotnet test tests/ArchLinterNet.CEL.Tests --no-restore` is run
- **THEN** all CEL tests pass

