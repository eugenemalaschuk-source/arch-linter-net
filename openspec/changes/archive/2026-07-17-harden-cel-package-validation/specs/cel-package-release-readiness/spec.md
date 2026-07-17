## MODIFIED Requirements

### Requirement: CEL package is dependency-clean

The packed `ArchLinterNet.CEL` `.nuspec` SHALL declare no NuGet package dependencies. The packed `ArchLinterNet.CEL` `.nupkg` SHALL contain `README.md`, `lib/net10.0/ArchLinterNet.CEL.dll`, and `lib/net10.0/ArchLinterNet.CEL.xml`, and SHALL NOT contain any `ArchLinterNet.Core`, `ArchLinterNet.Cli`, or `ArchLinterNet.Testing` assembly, nor any YAML, JSON-schema, Buildalyzer, or Roslyn asset.

#### Scenario: CEL nuspec has no dependencies
- **WHEN** `nupkg/ArchLinterNet.CEL.*.nupkg` is unpacked and its `.nuspec` inspected
- **THEN** it contains no `<dependency>` elements

#### Scenario: CEL package contains its required entries
- **WHEN** `nupkg/ArchLinterNet.CEL.*.nupkg`'s archive listing is inspected
- **THEN** it contains `README.md`, `lib/net10.0/ArchLinterNet.CEL.dll`, and `lib/net10.0/ArchLinterNet.CEL.xml`

#### Scenario: CEL package rejects forbidden content
- **WHEN** `nupkg/ArchLinterNet.CEL.*.nupkg`'s archive listing is inspected
- **THEN** it contains no `ArchLinterNet.Core`/`ArchLinterNet.Cli`/`ArchLinterNet.Testing` assembly and no `YamlDotNet`, `JsonSchema.Net`, `Buildalyzer`, or `Microsoft.CodeAnalysis` asset

## ADDED Requirements

### Requirement: Core depends on a version-matching, non-embedded CEL package

`ArchLinterNet.Core`'s packed `.nuspec` SHALL declare exactly one `ArchLinterNet.CEL` dependency whose version equals the packed `ArchLinterNet.CEL` package's own version. The packed `ArchLinterNet.Core` `.nupkg` SHALL NOT contain `lib/net10.0/ArchLinterNet.CEL.dll`.

#### Scenario: Core nuspec declares a version-matching CEL dependency
- **WHEN** `nupkg/ArchLinterNet.Core.*.nupkg`'s `.nuspec` and `nupkg/ArchLinterNet.CEL.*.nupkg`'s `.nuspec` are both inspected
- **THEN** Core's declared `ArchLinterNet.CEL` dependency version equals CEL's packed `<version>`

#### Scenario: Core package does not embed the CEL assembly
- **WHEN** `nupkg/ArchLinterNet.Core.*.nupkg`'s archive listing is inspected
- **THEN** it does not contain `lib/net10.0/ArchLinterNet.CEL.dll`
