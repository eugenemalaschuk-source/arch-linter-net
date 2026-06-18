## 1. Python tooling setup

- [ ] 1.1 Create `tools/pyproject.toml` with mkdocs and mkdocs-material as dependencies, following First Ice pattern (`[tool.uv] package = false`)
- [ ] 1.2 Run `uv sync --project tools/pyproject.toml` to generate `.venv` and commit `tools/uv.lock`
- [ ] 1.3 Append `.venv/` to `.gitignore`

## 2. MkDocs configuration

- [ ] 2.1 Create `mkdocs.yml` at repository root with mkdocs-material theme and full navigation structure
- [ ] 2.2 Add `make venv`, `make docs-serve`, `make docs-build` targets to `Makefile`
- [ ] 2.3 Update Makefile header comment to document new targets

## 3. Core documentation pages

- [ ] 3.1 Create `docs/index.md` — overview, positioning, and key features
- [ ] 3.2 Create `docs/getting-started.md` — quick start guide with minimal example
- [ ] 3.3 Create `docs/installation.md` — installation via dotnet tool, NuGet, and CI
- [ ] 3.4 Create `docs/cli.md` — CLI commands, options, and usage examples
- [ ] 3.5 Create `docs/policy-format.md` — YAML policy file structure, blocks, and examples
- [ ] 3.6 Create `docs/contracts/index.md` — overview of all contract families with behavior descriptions

## 4. Guides section

- [ ] 4.1 Create `docs/guides/ci-integration.md` — CI workflow integration guide
- [ ] 4.2 Create `docs/guides/migration-baselines.md` — frozen debt, ignored violations, audit vs strict

## 5. AI and reference section

- [ ] 5.1 Create `docs/ai/index.md` — placeholder/entry point for AI-facing policy authoring docs (from #662)
- [ ] 5.2 Create `docs/reference/yaml-schema.md` — complete YAML schema reference
- [ ] 5.3 Create `docs/reference/release-process.md` — release process documentation

## 6. Validation

- [ ] 6.1 Run `make venv` and verify `.venv` is created successfully
- [ ] 6.2 Run `make docs-build` and verify site builds without errors
- [ ] 6.3 Verify all navigation links resolve correctly
