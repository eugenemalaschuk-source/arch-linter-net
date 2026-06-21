# NuGet Package Metadata

ArchLinterNet packages are published to NuGet.org. NuGet package metadata is part of the public product documentation surface and must not point users to internal project-maintenance docs.

## Canonical link model

| Metadata field | Target |
|----------------|--------|
| `PackageProjectUrl` / `ProjectUrl` | Public product documentation site on GitHub Pages. |
| `RepositoryUrl` | GitHub repository. |
| `RepositoryType` | `git`. |
| `PackageReadmeFile` | Concise user-facing package README. |
| `PackageLicenseExpression` | Repository license expression. |
| `PackageTags` | Product discovery tags such as architecture, linting, dotnet, yaml, ci. |

## Current expected values

```xml
<PackageProjectUrl>https://eugenemalaschuk-source.github.io/arch-linter-net/</PackageProjectUrl>
<PackageReadmeFile>README.md</PackageReadmeFile>
<PackageLicenseExpression>MIT</PackageLicenseExpression>
<RepositoryUrl>https://github.com/eugenemalaschuk-source/arch-linter-net</RepositoryUrl>
<RepositoryType>git</RepositoryType>
```

The GitHub Pages site is the canonical documentation URL after Pages publication is enabled. The GitHub repository remains the source-code URL.

## Package README rule

The packaged README should be short and user-facing. It should explain:

- what the tool does;
- quick start;
- public docs links;
- main capabilities;
- non-goals;
- repository and license.

It must not include internal backlog governance, OpenSpec archive details, repository-agent instructions, or implementation planning notes.

## Do not link as product docs

NuGet.org documentation links must not point to:

- `docs/internal/`;
- OpenSpec change archives;
- backlog governance or issue-writing rules;
- `AGENTS.md` or maintenance-agent instructions;
- private release-operation notes that are not useful for users.

Internal Markdown may remain visible in the GitHub repository, but it is not the product documentation surface.

## Release checklist

Before publishing packages:

1. Inspect at least one generated `.nupkg`.
1. Confirm the project URL opens the public product docs.
1. Confirm the repository URL opens the GitHub repository.
1. Confirm the package README is concise and user-facing.
1. Confirm the license expression matches the repository license.
1. Confirm release notes are user-facing enough for NuGet.org.
