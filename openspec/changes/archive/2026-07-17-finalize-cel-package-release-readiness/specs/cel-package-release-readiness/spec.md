## ADDED Requirements

### Requirement: CEL package ships public API XML documentation
`ArchLinterNet.CEL` SHALL generate an XML documentation file at build time and pack it into `ArchLinterNet.CEL.*.nupkg`.

#### Scenario: XML doc file is produced and packed
- **WHEN** `dotnet pack src/ArchLinterNet.CEL/ArchLinterNet.CEL.csproj` is run
- **THEN** the packed `.nupkg` contains an `ArchLinterNet.CEL.xml` documentation file alongside the assembly

### Requirement: CEL package ships a package-specific README
`ArchLinterNet.CEL` SHALL pack a README describing the Profile v1 language subset, the compile/evaluate lifecycle, limits, diagnostics, thread-safety, and non-goals, distinct from the shared repository root README used by the other packages.

#### Scenario: CEL README is packed and describes usage
- **WHEN** `nupkg/ArchLinterNet.CEL.*.nupkg` is unpacked
- **THEN** its `README.md` describes `ArchLinterNet.CEL` specifically, including a compile/evaluate code example, and is not the shared repository root README

### Requirement: CEL package is dependency-clean
The packed `ArchLinterNet.CEL` `.nuspec` SHALL declare no NuGet package dependencies.

#### Scenario: CEL nuspec has no dependencies
- **WHEN** `nupkg/ArchLinterNet.CEL.*.nupkg` is unpacked and its `.nuspec` inspected
- **THEN** it contains no `<dependency>` elements

### Requirement: Packed CEL artifact is externally consumable
An external consumer project referencing only the packed `ArchLinterNet.CEL` `.nupkg` (not a `ProjectReference`) SHALL be able to define a schema, compile a Profile v1 predicate, and evaluate it successfully.

#### Scenario: External consumer smoke test passes
- **WHEN** a throwaway console project adds a `PackageReference` to the locally packed `ArchLinterNet.CEL` `.nupkg` and runs a program that builds a schema, compiles a Profile v1 predicate, and evaluates it
- **THEN** the program builds and runs successfully and produces the expected evaluation result

### Requirement: Internal blueprint documents the public API stability boundary
`docs/internal/cel-engine-architecture.md` SHALL contain a section enumerating each of the 9 v1-excluded public-API concepts identified in #324 (parser AST exposure, arbitrary CLR object binding, reflection and `dynamic`, raw `IDictionary<string, object?>` activations, unrestricted delegates/expression trees, public function registration, process-global cache ownership, unbounded evaluation, mutable environments), and for each SHALL record its future architectural direction rather than only stating it is unsupported.

#### Scenario: Public API stability boundary section exists
- **WHEN** `docs/internal/cel-engine-architecture.md` is read
- **THEN** it contains a section titled "Public API stability boundary" listing all 9 exclusions, each with a documented future direction

### Requirement: Internal blueprint documents an extension governance checklist
`docs/internal/cel-engine-architecture.md` SHALL contain a section with the 10-question extension governance checklist from #330 that every future extension proposal must answer before implementation.

#### Scenario: Extension governance checklist section exists
- **WHEN** `docs/internal/cel-engine-architecture.md` is read
- **THEN** it contains a section titled "Extension governance checklist" with all 10 questions from the issue

### Requirement: Internal blueprint documents versioning and API-compatibility policy
`docs/internal/cel-engine-architecture.md` SHALL contain a section documenting: profile versioning rules (how a new `CelProfile` version is introduced without breaking Profile v1 identity), package release SemVer rules (cross-referencing `docs/reference/release-process.md`), and the existing API-compatibility baseline mechanism (`CelPublicApiSurfaceApprovalTests.cs`) that gates unintentional public API changes.

#### Scenario: Versioning and compatibility policy section exists
- **WHEN** `docs/internal/cel-engine-architecture.md` is read
- **THEN** it contains a section documenting profile versioning, package release SemVer, and the public API surface approval-test baseline mechanism
