## MODIFIED Requirements

### Requirement: Make targets for documentation workflow
The project SHALL define these targets:
- `make venv` — creates the Python virtual environment via `uv sync --project tools/pyproject.toml`
- `make docs-serve` — starts a local MkDocs development server
- `make docs-build` — builds the static documentation site
- `make fmt-docs` — auto-formats markdown documentation with mdformat
- `make lint-evergreen-docs` — rejects ArchLinterNet product-release SemVer as an evergreen public docs identity while allowing genuine machine/standard/release-process version semantics
- `make lint-canonical-actions-pinning` — rejects a mutable third-party GitHub Actions `uses:` ref inside a canonical public workflow-example code fence, while allowing first-party local/reusable-workflow refs and non-workflow prose
- `make lint-docs` — runs the evergreen-docs guard, the canonical-actions-pinning guard, and strict MkDocs validation

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

#### Scenario: lint-docs rejects a mutable canonical Actions ref
- **WHEN** a canonical public workflow-example code fence contains a third-party `uses: owner/action@vN` (or other mutable tag/branch) reference
- **THEN** `make lint-docs` fails before accepting the documentation change

#### Scenario: lint-docs accepts an immutable canonical Actions pin
- **WHEN** a canonical public workflow-example code fence pins a third-party action to a full commit SHA, optionally with a trailing human-readable version comment
- **THEN** the canonical-actions-pinning guard does not reject that reference

## ADDED Requirements

### Requirement: Canonical GitHub Actions examples pin third-party actions to immutable SHAs
Canonical, copy/paste-intended public GitHub Actions workflow examples in `docs/` SHALL pin every
third-party action `uses:` reference to a full immutable commit SHA rather than a mutable
version tag or branch. A human-readable version comment MAY appear beside the pin. First-party
local actions (`./.github/actions/...`) and reusable-workflow references owned by this
repository are not required to use SHA syntax. Prose that mentions an action version
descriptively, outside a workflow-example code fence, is not a canonical pin and is not subject
to this requirement.

#### Scenario: Canonical example reuses a reviewed production pin
- **WHEN** a canonical documentation example uses the same third-party action and version already
  pinned in a production `.github/workflows/*.yml` file
- **THEN** the documentation example pins that action to the same reviewed commit SHA

#### Scenario: Local action is not forced into SHA syntax
- **WHEN** a canonical workflow example references a first-party local action under
  `./.github/actions/...`
- **THEN** the lint does not require that reference to use commit-SHA syntax

#### Scenario: Descriptive prose is not a false positive
- **WHEN** documentation prose mentions `actions/checkout@v4` descriptively outside a workflow
  code fence, such as in historical or explanatory text
- **THEN** the lint does not treat that mention as a canonical unpinned reference
