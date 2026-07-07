# Project Metadata Policy Format

Project metadata contracts validate statically declared project metadata discovered from `.csproj` files.

## Contract groups

- `strict_project_metadata`
- `audit_project_metadata`

## YAML shape

```yaml
contracts:
  strict_project_metadata:
    - id: <string>                         # Optional — stable identifier
      name: <string>                       # Required
      projects: [<project-path>]           # Required — repo-relative discovered .csproj paths
      required_properties:                 # Optional — exact value requirements
        <PropertyName>: <value>
      forbidden_properties:                # Optional — exact forbidden value pairs
        <PropertyName>: <value>
      allowed_friend_assemblies: []        # Optional — explicit allowlist for InternalsVisibleTo
      forbidden_project_references: []     # Optional — project path globs matched against ProjectReference targets
      reason: <string>                     # Recommended
```

At least one of `required_properties`, `forbidden_properties`, `allowed_friend_assemblies`, or `forbidden_project_references` must be present.

## Example

```yaml
analysis:
  solution: ArchLinterNet.slnx

contracts:
  strict_project_metadata:
    - id: package-project-defaults
      name: package-project-defaults
      projects:
        - src/ArchLinterNet.Core/ArchLinterNet.Core.csproj
        - src/ArchLinterNet.Cli/ArchLinterNet.Cli.csproj
      required_properties:
        Nullable: enable
        TreatWarningsAsErrors: true
      allowed_friend_assemblies:
        - ArchLinterNet.Core.Tests
      reason: Package-facing projects must preserve packaging and visibility defaults.

    - id: production-projects-must-not-reference-tests
      name: production-projects-must-not-reference-tests
      projects:
        - src/ArchLinterNet.Core/ArchLinterNet.Core.csproj
      forbidden_project_references:
        - tests/**/*.csproj
      reason: Production projects must not depend on test projects.
```

## Matching rules

- `projects` entries match discovered projects by repo-relative `.csproj` path.
- `required_properties` and `forbidden_properties` compare exact scalar property values, case-insensitively.
- `allowed_friend_assemblies` compares exact friend assembly names from `InternalsVisibleTo Include="..."`.
- `forbidden_project_references` uses the same project-path glob matching as `analysis.project_include` / `analysis.project_exclude`.

## Discovery requirements

This contract family depends on project discovery. Configure one of:

```yaml
analysis:
  solution: MyApp.slnx
```

or:

```yaml
analysis:
  projects:
    - src/MyApp/MyApp.csproj
```

Without discovery, the contract cannot evaluate and `CheckConfiguration` reports `no project metadata discovered`.

## Current limits

- Static project parsing only — no MSBuild target execution.
- Inherited scalar properties are resolved from the nearest `Directory.Build.props` chain when that file structure is statically readable.
- Complex conditional property evaluation and arbitrary import graphs are out of scope for this first version.
