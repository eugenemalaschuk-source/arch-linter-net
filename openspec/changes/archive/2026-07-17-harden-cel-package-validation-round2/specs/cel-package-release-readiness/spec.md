## MODIFIED Requirements

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

### Requirement: Internal blueprint documents versioning and API-compatibility policy

`docs/internal/cel-engine-architecture.md` SHALL contain a section documenting: profile versioning rules (how a new `CelProfile` version is introduced without breaking Profile v1 identity), package release SemVer rules (cross-referencing `docs/reference/release-process.md`), and the existing API-compatibility baseline mechanism (`CelPublicApiSurfaceApprovalTests.cs`) that gates unintentional public API changes. The package release SemVer rules SHALL NOT contradict the profile versioning rules — in particular, they SHALL NOT describe a Profile v1 semantics change as an allowed release event, since Profile v1 is documented elsewhere in the same section as permanently frozen.

#### Scenario: Versioning and compatibility policy section exists
- **WHEN** `docs/internal/cel-engine-architecture.md` is read
- **THEN** it contains a section documenting profile versioning, package release SemVer, and the public API surface approval-test baseline mechanism

#### Scenario: Versioning policy is internally consistent
- **WHEN** `docs/internal/cel-engine-architecture.md`'s "Profile versioning" and "Package release versioning" subsections are read together
- **THEN** neither subsection implies that a Profile v1 semantics change is permitted under any version-bump scenario; only introducing a new profile identity (e.g. v2) is described as a semantics-changing release event
