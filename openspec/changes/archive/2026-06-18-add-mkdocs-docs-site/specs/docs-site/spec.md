## ADDED Requirements

### Requirement: Documentation tooling setup
The repository SHALL contain a Python tooling project at `tools/pyproject.toml` that defines MkDocs and mkdocs-material as dependencies, managed via `uv` with a committed `tools/uv.lock` file.

#### Scenario: uv sync resolves dependencies
- **WHEN** running `uv sync --project tools/pyproject.toml`
- **THEN** a `.venv` directory is created at the repository root with mkdocs and mkdocs-material installed

#### Scenario: uv.lock is committed
- **WHEN** inspecting the repository
- **THEN** `tools/uv.lock` is tracked by git

### Requirement: .venv is gitignored
The `.gitignore` file SHALL contain an entry for `.venv/` to prevent the virtual environment from being committed.

#### Scenario: .venv excluded from git
- **WHEN** running `git status`
- **THEN** `.venv/` is not shown as an untracked directory

### Requirement: MkDocs configuration
The repository SHALL contain an `mkdocs.yml` at the project root that configures the mkdocs-material theme and defines navigation for all documentation pages.

#### Scenario: mkdocs.yml exists
- **WHEN** inspecting the repository root
- **THEN** `mkdocs.yml` is present with `theme: name: material`

#### Scenario: Navigation is configured
- **WHEN** viewing the built site
- **THEN** the navigation sidebar contains links to Home, Getting Started, Installation, CLI, Policy Format, Contracts, CI Integration, Migration Baselines, AI section, YAML Schema, and Release Process

### Requirement: Documentation pages
The repository SHALL contain the following documentation pages under `docs/`:
- `docs/index.md` — overview and positioning
- `docs/getting-started/index.md` — quick start guide
- `docs/installation/index.md` — installation instructions
- `docs/cli/index.md` — CLI usage reference
- `docs/policy-format/index.md` — policy file structure
- `docs/contracts/index.md` — contract family overview
- `docs/guides/ci-integration.md` — CI integration guide
- `docs/guides/migration-baselines.md` — frozen debt and ignored violations
- `docs/ai/index.md` — AI section entry point (placeholder for #662)
- `docs/reference/yaml-schema.md` — YAML schema reference
- `docs/reference/release-process.md` — release process documentation
- `docs/internal/README.md` — contributor documentation (excluded from site build)

#### Scenario: All required pages exist
- **WHEN** listing files under `docs/`
- **THEN** all of the above files exist

### Requirement: Make targets for documentation workflow
The project SHALL define these targets:
- `make venv` — creates the Python virtual environment via `uv sync --project tools/pyproject.toml`
- `make docs-serve` — starts a local MkDocs development server
- `make docs-build` — builds the static documentation site
- `make fmt-docs` — auto-formats markdown documentation with mdformat
- `make lint-docs` — runs `mkdocs build --strict` to verify documentation structure

#### Scenario: make venv creates virtual environment
- **WHEN** running `make venv`
- **THEN** `.venv` directory is created at the project root with all dependencies

#### Scenario: make docs-build produces site output
- **WHEN** running `make docs-build` after `make venv`
- **THEN** a `site/` directory is generated containing the built HTML documentation

### Requirement: Contributor documentation is separated from user docs
Project/contributor documentation SHALL live in `docs/internal/` to distinguish it from user-facing MkDocs pages. The `docs/internal/` directory SHALL be excluded from the MkDocs site build.

#### Scenario: internal docs excluded from site
- **WHEN** running `mkdocs build`
- **THEN** pages under `docs/internal/` are not published to the output site

### Requirement: Documentation builds without errors
The documentation site SHALL build successfully with zero warnings or errors.

#### Scenario: Clean build succeeds
- **WHEN** running `make docs-build`
- **THEN** the command exits with code 0
