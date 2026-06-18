## Why

ArchLinterNet needs a proper user-facing documentation site. Currently, the only documentation is the project `README.md` (focused on contributors/developers) and the `docs/README.md` (internal API reference). Users have no navigable guide for installation, CLI usage, policy authoring, CI integration, or migration workflows.

This mirrors the First Ice documentation approach and satisfies issue #663, which blocks the public package readiness track.

## What Changes

- Create a Python tooling project at `tools/pyproject.toml` with MkDocs and mkdocs-material as dependencies, managed via `uv`
- Commit `tools/uv.lock` after first dependency resolution
- Add `.venv/` to `.gitignore`
- Create `mkdocs.yml` with mkdocs-material theme and full navigation structure
- Create 10+ documentation pages covering getting started, installation, CLI, policy format, contract families, CI integration, migration baselines, AI section placeholder, YAML schema reference, and release process
- Add `make venv`, `make docs-serve`, `make docs-build` targets to `Makefile`
- Keep existing `docs/README.md` as project/contributor documentation (not user docs)

## Capabilities

### New Capabilities
- `docs-site`: MkDocs-based documentation site with mkdocs-material theme, Python tooling via uv, and Make targets for local development and CI builds

### Modified Capabilities
*(none — no existing specs)*

## Impact

- **New dependencies**: Python packages `mkdocs`, `mkdocs-material` managed via `tools/pyproject.toml` + `uv.lock`
- **New files**: `tools/pyproject.toml`, `tools/uv.lock`, `mkdocs.yml`, ~12 markdown pages under `docs/`
- **Modified files**: `.gitignore`, `Makefile`
- **Runtime**: No impact — documentation is a developer/CI concern only
- **CI**: Separate from package build/release workflow
