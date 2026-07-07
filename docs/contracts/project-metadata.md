# Project Metadata Contracts

Project metadata contracts enforce rules over statically declared project and assembly metadata surfaces: selected MSBuild properties, `InternalsVisibleTo` declarations, and declared `ProjectReference` targets discovered from `.csproj` files.

Two families are covered on this page:

- **Project metadata** (`strict_project_metadata`/`audit_project_metadata`) — selected discovered projects must satisfy required property values, must not carry forbidden property values, may only expose explicitly allowed friend assemblies, and must not declare forbidden project references.

These contracts operate on project discovery output, so they require `analysis.solution` or `analysis.projects` to discover and parse matching `.csproj` files.

## Example

```yaml
analysis:
  solution: ArchLinterNet.slnx

contracts:
  strict_project_metadata:
    - id: core-project-metadata
      name: core-project-metadata
      projects:
        - src/ArchLinterNet.Core/ArchLinterNet.Core.csproj
      required_properties:
        Nullable: enable
        TreatWarningsAsErrors: true
      allowed_friend_assemblies:
        - ArchLinterNet.Core.Tests
      forbidden_project_references:
        - tests/**/*.csproj
      reason: Core packaging and friend-assembly boundaries must stay intentional.
```

## Semantics

Project metadata contracts match discovered projects by their repo-relative `.csproj` path.

**Required properties**: every entry in `required_properties` must be present on the discovered project with the exact configured value. The value may come from the project file itself or from the nearest applicable `Directory.Build.props` chain that discovery can parse statically.

**Forbidden properties**: every entry in `forbidden_properties` forbids that exact property/value pair. A project that sets the same value is a violation.

**Allowed friend assemblies**: when `allowed_friend_assemblies` is non-empty, every discovered `InternalsVisibleTo` declaration must appear in that allowlist. Omitted allowlists mean "do not check friend assemblies."

**Forbidden project references**: every pattern in `forbidden_project_references` is matched against the discovered project's repo-relative referenced `.csproj` paths using the same project-path glob semantics as `analysis.project_include` / `analysis.project_exclude`.

Violations identify the project path, the metadata kind (`required_property`, `forbidden_property`, `friend_assembly`, or `project_reference`), the relevant key/value or matched path, and the source file path when discovery can determine it.

## Configuration validation

Two safety checks run before execution:

- **Missing project selector.** A contract with no non-blank `projects` entry fails policy loading.
- **Missing expectation.** A contract with no `required_properties`, `forbidden_properties`, `allowed_friend_assemblies`, or `forbidden_project_references` fails policy loading.

`CheckConfiguration` also reports a visible `no project metadata discovered` configuration violation when a configured project path is not present in project discovery output. This avoids a silent false green where the contract can never evaluate because no `.csproj` metadata was discovered for that path.

## Scope: what is and is not covered here

Project metadata contracts are static `.csproj`/`Directory.Build.props` analysis only:

- they do check statically declared scalar properties, friend assemblies, and declared `ProjectReference` items;
- they do not run MSBuild targets or build the project;
- they do not perform full conditional/import evaluation for every possible MSBuild construct;
- they do not replace package validation, signing validation, or runtime behavior checks.
