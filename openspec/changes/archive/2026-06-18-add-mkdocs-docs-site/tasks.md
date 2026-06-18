## 1. Python tooling setup

- [x] 1.1 Create `tools/pyproject.toml` with mkdocs and mkdocs-material as dependencies, following First Ice pattern (`[tool.uv] package = false`)
- [x] 1.2 Run `uv sync --project tools/pyproject.toml` to generate `.venv` and commit `tools/uv.lock`
- [x] 1.3 Append `.venv/` to `.gitignore`

## 2. MkDocs configuration

- [x] 2.1 Create `mkdocs.yml` at repository root with mkdocs-material theme and full navigation structure
- [x] 2.2 Add `make venv`, `make docs-serve`, `make docs-build` targets to `Makefile`
- [x] 2.3 Update Makefile header comment to document new targets

## 3. Core documentation pages

- [x] 3.1 Create `docs/index.md` — overview, positioning, and key features
- [x] 3.2 Create `docs/getting-started/index.md` — quick start guide with minimal example
- [x] 3.3 Create `docs/installation/index.md` — installation via dotnet tool, NuGet, and CI
- [x] 3.4 Create `docs/cli/index.md` — CLI commands, options, and usage examples
- [x] 3.5 Create `docs/policy-format/index.md` — YAML policy file structure, blocks, and examples
- [x] 3.6 Create `docs/contracts/index.md` — overview of all contract families with behavior descriptions

## 4. Guides section

- [x] 4.1 Create `docs/guides/ci-integration.md` — CI workflow integration guide
- [x] 4.2 Create `docs/guides/migration-baselines.md` — frozen debt, ignored violations, audit vs strict

## 5. AI and reference section

- [x] 5.1 Create `docs/ai/index.md` — placeholder/entry point for AI-facing policy authoring docs (from #662)
- [x] 5.2 Create `docs/reference/yaml-schema.md` — complete YAML schema reference
- [x] 5.3 Create `docs/reference/release-process.md` — release process documentation

## 6. Validation

- [x] 6.1 Run `make venv` and verify `.venv` is created successfully
- [x] 6.2 Run `make docs-build` and verify site builds without errors
- [x] 6.3 Verify all navigation links resolve correctly
