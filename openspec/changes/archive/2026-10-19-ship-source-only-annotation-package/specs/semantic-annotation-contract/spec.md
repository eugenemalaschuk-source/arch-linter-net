## ADDED Requirements

### Requirement: Packed annotation package is source-only and externally consumable

The `ArchLinterNet.Annotations` package SHALL include the approved annotation C# source set under `contentFiles/cs/any` with NuGet `buildAction=Compile` and `copyToOutput=false`. It SHALL include one catalog identity source whose generation and exact SemVer match the package being packed. The `.nupkg` SHALL NOT contain `lib/`, `ref/`, `runtimes/`, `native/`, `build/`, `buildTransitive/`, analyzer, source-generator, or executable tool assets, and SHALL have no ArchLinterNet or other runtime package dependency. The source files SHALL be compile-injected by a direct `PackageReference` into each opting-in SDK-style project.

#### Scenario: A packed source package has only compile content and package metadata

- **WHEN** the repository packs `ArchLinterNet.Annotations`
- **THEN** the package contains each approved source file exactly once as `contentFiles/cs/any` content, declares those files for compilation without copying them to output, and contains no runtime, binary, analyzer, generator, build-target, or tool asset

#### Scenario: Package and assembly catalog identities agree

- **WHEN** an SDK-style consumer compiles source from a packed annotation package
- **THEN** its assembly carries exactly one internal catalog identity with generation 1 and the exact SemVer of the package that supplied the source

#### Scenario: A public library opts into canonical annotations

- **WHEN** an external public class library directly references the packed package and uses canonical role and context attributes
- **THEN** it builds through ordinary restore/build, Core recognizes the manifest FQNs, its exported public API contains no package-provided annotation types, and its runtime dependency closure contains no annotation assembly

#### Scenario: Central package management and multiple projects opt in

- **WHEN** two SDK-style projects directly reference the package with a version centralized in `Directory.Packages.props`
- **THEN** both projects compile their own internal source definitions, remain discoverable by exact metadata name, and require neither shared CLR type identity nor a package runtime dependency

#### Scenario: Removing the package leaves no generated project artifacts

- **WHEN** a consumer removes its direct package reference, removes annotation usages, restores, cleans, and rebuilds
- **THEN** its project tree contains no package-generated source files and its build/runtime outputs contain no ArchLinterNet annotation assembly or lingering package artifacts

#### Scenario: Release inventory adds annotations only to the compatible stable line

- **WHEN** a release candidate has a stable version in `[0.10.0,0.11.0)`
- **THEN** its immutable package inventory records the exact-version annotation `.nupkg` once without requiring a symbol package, while previews and versions outside the reviewed range retain their reviewed package set

#### Scenario: Unity source import uses the same package source set

- **WHEN** a Unity-compatible project cannot consume NuGet `contentFiles`
- **THEN** the package documentation identifies a deterministic source-import path that uses the same reviewed C# files and does not define a second annotation vocabulary
