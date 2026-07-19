## MODIFIED Requirements

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
- `docs/ai/index.md` — AI section entry point (placeholder for #662)
- `docs/reference/yaml-schema.md` — YAML schema reference
- `docs/reference/release-process.md` — release process documentation
- `docs/internal/README.md` — contributor documentation (excluded from site build)

#### Scenario: All required pages exist
- **WHEN** listing files under `docs/`
- **THEN** all of the above files exist

#### Scenario: CEL guide is under Policy Authoring navigation
- **WHEN** viewing the built site's navigation sidebar
- **THEN** `docs/policy-format/cel-expressions.md` appears under the Policy Authoring section

### Requirement: Documentation builds without errors
The documentation site SHALL build successfully with zero warnings or errors.

#### Scenario: Clean build succeeds
- **WHEN** running `make docs-build`
- **THEN** the command exits with code 0

#### Scenario: CEL guide builds without broken links
- **WHEN** running `make lint-docs` (`mkdocs build --strict`) after the CEL guide and its cross-links are added
- **THEN** the command exits with code 0, with no broken internal links to or from `docs/policy-format/cel-expressions.md`
