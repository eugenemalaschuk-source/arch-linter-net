# Documentation Boundary

ArchLinterNet has two documentation domains.

## Public product documentation

Public product documentation is the only documentation published through MkDocs and GitHub Pages.

It includes:

- product overview and positioning;
- installation and quick start;
- CLI usage;
- policy format and YAML schema;
- supported contract families;
- CI integration;
- migration baseline usage;
- product-facing AI policy authoring guidance;
- supported capabilities and non-goals;
- product troubleshooting.

Public docs live under `docs/` and appear in `mkdocs.yml` navigation.

## Internal project documentation

Internal documentation is repository-maintenance material and is not published as product documentation.

It includes:

- backlog governance;
- issue-writing rules;
- OpenSpec/change archives;
- implementation planning notes;
- repository-agent instructions;
- internal release-operation details that are not useful to product users.

Internal docs may live under `docs/internal/`, `openspec/`, `.github/`, or root governance files.

## Publishing rule

GitHub Pages publishes only the MkDocs-generated public product site. Internal documentation remains accessible as GitHub Markdown but must not appear in public navigation or NuGet-facing product links.

`mkdocs.yml` excludes `docs/internal/` through `exclude_docs`.
