# Docs Site Specification

## Purpose
Sets up the MkDocs-based documentation site tooling, including a gitignored Python virtual environment.
## Requirements
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
- **THEN** the navigation sidebar contains links to Home, Getting Started, Installation, CLI, Policy Format, Contracts, CI Integration, Migration Baselines, the evergreen adoption/upgrade guide, AI section, YAML Schema, and Release Process

#### Scenario: Navigation identities are evergreen
- **WHEN** inspecting public MkDocs navigation
- **THEN** ordinary adoption, migration, reference, and guide labels/routes use durable concept names rather than an ArchLinterNet package release number

### Requirement: Documentation pages
The repository SHALL contain the following documentation pages under `docs/`:
- `docs/index.md` — overview and positioning
- `docs/getting-started/index.md` — quick start guide
- `docs/installation/index.md` — installation instructions
- `docs/cli/index.md` — CLI usage reference
- `docs/policy-format/index.md` — policy file structure
- `docs/policy-format/cel-expressions.md` — canonical public CEL policy expression guide
- `docs/contracts/index.md` — contract family overview
- `docs/guides/ci-integration.md` — CI integration guide
- `docs/guides/migration-baselines.md` — frozen debt and ignored violations
- `docs/guides/upgrading.md` — canonical evergreen adoption and upgrade guide
- `docs/guides/reference-entrypoints.md` — evergreen consumer wrapper/CI reference
- `docs/ai/index.md` — AI section entry point
- `docs/reference/yaml-schema.md` — YAML schema reference
- `docs/reference/release-process.md` — release process documentation
- `docs/internal/README.md` — contributor documentation (excluded from site build)

Version-specific product release-note or migration pages SHALL NOT be required public documentation files. Historical product release records belong in GitHub Releases/tags and other explicit release records.

#### Scenario: All required pages exist
- **WHEN** listing files under `docs/`
- **THEN** all of the above files exist

#### Scenario: CEL guide is under Policy Authoring navigation
- **WHEN** viewing the built site's navigation sidebar
- **THEN** `docs/policy-format/cel-expressions.md` appears under the Policy Authoring section

### Requirement: Make targets for documentation workflow
The project SHALL define these targets:
- `make venv` — creates the Python virtual environment via `uv sync --project tools/pyproject.toml`
- `make docs-serve` — starts a local MkDocs development server
- `make docs-build` — builds the static documentation site
- `make fmt-docs` — auto-formats markdown documentation with mdformat
- `make lint-evergreen-docs` — rejects ArchLinterNet product-release SemVer as an evergreen public docs identity while allowing genuine machine/standard/release-process version semantics
- `make lint-docs` — runs the evergreen-docs guard and strict MkDocs validation

#### Scenario: make venv creates virtual environment
- **WHEN** running `make venv`
- **THEN** `.venv` directory is created at the project root with all dependencies

#### Scenario: make docs-build produces site output
- **WHEN** running `make docs-build` after make venv
- **THEN** a `site/` directory is generated containing the built HTML documentation

#### Scenario: lint-docs rejects a version-named evergreen page
- **WHEN** a public guide/route/navigation identity embeds an ArchLinterNet product release SemVer
- **THEN** `make lint-docs` fails before accepting the documentation change

#### Scenario: lint-docs retains real contract versions
- **WHEN** documentation contains a genuine machine/document/standard version such as a schema/artifact identity or SARIF version
- **THEN** the evergreen guard does not reject that version merely because it is numeric

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

#### Scenario: CEL guide builds without broken links
- **WHEN** running `make lint-docs` after the CEL guide and its cross-links are added
- **THEN** the command exits with code 0, with no broken internal links to or from `docs/policy-format/cel-expressions.md`

### Requirement: Policy import documentation is publicly discoverable
The MkDocs navigation and core public entry pages SHALL link to the policy-import guide, and the README capability summary SHALL identify deterministic local policy imports without linking users to internal design documents.

#### Scenario: User looks for policy imports
- **WHEN** a user starts from the README, policy-format overview, YAML schema reference, troubleshooting page, or Policy Authoring navigation
- **THEN** the public policy-import guide is reachable without consulting `docs/internal`

### Requirement: Packaged schema reference uses installed contract discovery
The public documentation SHALL explain that persisted schema/document identities are independently versioned compatibility contracts and that an installed release's `schema list` / `schema print` output is the authority for the exact shipped identifiers.

The YAML schema reference MAY document exact immutable schema IDs when those IDs are themselves the compatibility subject. Evergreen README, adoption, installation, navigation, CLI-overview, and AI guidance SHALL NOT present one package SemVer as the permanent or "current package line" identity for the documentation. Product package SemVer SHALL NOT be mechanically transformed into a schema URL.

#### Scenario: Adopter configures an editor for an installed release
- **WHEN** an adopter follows the schema reference without cloning the repository
- **THEN** the documentation directs it to discover/print the exact packaged schema for the selected document role and may show an immutable packaged `$id` as reference evidence

#### Scenario: Adopter distinguishes product and schema versions
- **WHEN** an adopter reads schema guidance
- **THEN** it explains that schema/document identities evolve independently from package SemVer and that the installed schema registry, not a guessed package-version URL, is authoritative

### Requirement: New-debt CI workflow is documented
Public MkDocs guidance SHALL document invoking the architecture-debt gate, its strict/audit boundary, exact baseline lifecycle, optional policy-weakening integration, output and exit behavior, and fail-closed comparison limits. The examples SHALL distinguish matched/new/resolved/stale debt from independent weakening failures and SHALL state that baseline updates remain explicit review operations.

#### Scenario: CI adopter can compose the gate safely
- **WHEN** an adopter follows the CI integration and baseline migration guidance
- **THEN** they can run the gate with explicit baseline and policy-context artifacts without treating warnings or policy weakening as persistent baseline debt

### Requirement: Public self-dogfood reference is discoverable
The MkDocs site SHALL include an evergreen public guide for the real-repository
self-dogfood reference workflow in the Guides navigation. The route and
navigation label SHALL use a durable concept name rather than an ArchLinterNet
product release number.

#### Scenario: User finds the real-repository reference
- **WHEN** a user browses the public Guides navigation
- **THEN** they can open the self-dogfood reference without consulting
contributor-only documentation
