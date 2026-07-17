# cel-package-release-readiness Specification

## Purpose
TBD - created by archiving change finalize-cel-package-release-readiness. Update Purpose after archive.
## Requirements
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

The packed `ArchLinterNet.CEL` `.nuspec` SHALL declare no NuGet package dependencies. The packed `ArchLinterNet.CEL` `.nupkg` SHALL contain `README.md`, `lib/net10.0/ArchLinterNet.CEL.dll`, and `lib/net10.0/ArchLinterNet.CEL.xml`, and SHALL NOT contain any `ArchLinterNet.Core`, `ArchLinterNet.Cli`, or `ArchLinterNet.Testing` assembly, any YAML, JSON-schema, Buildalyzer, or Roslyn asset (by known library name), or any raw `.yml`, `.yaml`, or `.schema.json` file (by extension, regardless of origin).

#### Scenario: CEL nuspec has no dependencies
- **WHEN** `nupkg/ArchLinterNet.CEL.*.nupkg` is unpacked and its `.nuspec` inspected
- **THEN** it contains no `<dependency>` elements

#### Scenario: CEL package contains its required entries
- **WHEN** `nupkg/ArchLinterNet.CEL.*.nupkg`'s archive listing is inspected
- **THEN** it contains `README.md`, `lib/net10.0/ArchLinterNet.CEL.dll`, and `lib/net10.0/ArchLinterNet.CEL.xml`

#### Scenario: CEL package rejects forbidden content by library name
- **WHEN** `nupkg/ArchLinterNet.CEL.*.nupkg`'s archive listing is inspected
- **THEN** it contains no `ArchLinterNet.Core`/`ArchLinterNet.Cli`/`ArchLinterNet.Testing` assembly and no `YamlDotNet`, `JsonSchema.Net`, `Buildalyzer`, or `Microsoft.CodeAnalysis` asset

#### Scenario: CEL package rejects raw YAML/JSON-schema files by extension
- **WHEN** `nupkg/ArchLinterNet.CEL.*.nupkg`'s archive listing is inspected
- **THEN** it contains no entry ending in `.yml`, `.yaml`, or `.schema.json`, regardless of which library or process produced that entry

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

`docs/internal/cel-engine-architecture.md` SHALL contain a section documenting: profile versioning rules (how a new `CelProfile` version is introduced without breaking Profile v1 identity), package release SemVer rules (cross-referencing `docs/reference/release-process.md`), and the existing API-compatibility baseline mechanism (`CelPublicApiSurfaceApprovalTests.cs`) that gates unintentional public API changes. The package release SemVer rules SHALL NOT contradict the profile versioning rules — in particular, they SHALL NOT describe a Profile v1 semantics change as an allowed release event, since Profile v1 is documented elsewhere in the same section as permanently frozen.

#### Scenario: Versioning and compatibility policy section exists
- **WHEN** `docs/internal/cel-engine-architecture.md` is read
- **THEN** it contains a section documenting profile versioning, package release SemVer, and the public API surface approval-test baseline mechanism

#### Scenario: Versioning policy is internally consistent
- **WHEN** `docs/internal/cel-engine-architecture.md`'s "Profile versioning" and "Package release versioning" subsections are read together
- **THEN** neither subsection implies that a Profile v1 semantics change is permitted under any version-bump scenario; only introducing a new profile identity (e.g. v2) is described as a semantics-changing release event

### Requirement: Core depends on a version-matching, non-embedded CEL package

`ArchLinterNet.Core`'s packed `.nuspec` SHALL declare exactly one `ArchLinterNet.CEL` dependency whose version equals the packed `ArchLinterNet.CEL` package's own version. The packed `ArchLinterNet.Core` `.nupkg` SHALL NOT contain `lib/net10.0/ArchLinterNet.CEL.dll`.

#### Scenario: Core nuspec declares a version-matching CEL dependency
- **WHEN** `nupkg/ArchLinterNet.Core.*.nupkg`'s `.nuspec` and `nupkg/ArchLinterNet.CEL.*.nupkg`'s `.nuspec` are both inspected
- **THEN** Core's declared `ArchLinterNet.CEL` dependency version equals CEL's packed `<version>`

#### Scenario: Core package does not embed the CEL assembly
- **WHEN** `nupkg/ArchLinterNet.Core.*.nupkg`'s archive listing is inspected
- **THEN** it does not contain `lib/net10.0/ArchLinterNet.CEL.dll`

