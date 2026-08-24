.PHONY: venv docs-serve docs-build fmt-docs lint-docs lint-evergreen-docs lint-dogfood-reference-evidence lint-public-docs-contract test-public-docs-contract

venv:  ## Create local Python virtual environment via uv
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" sync --project tools/pyproject.toml

docs-serve:  ## Start local MkDocs development server
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml mkdocs serve

docs-build:  ## Build static documentation site
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml python tools/scripts/filter_mkdocs_warnings.py -- mkdocs build

fmt-docs:  ## Auto-format markdown documentation
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml mdformat docs/

lint-evergreen-docs:  ## Reject product release SemVer as an evergreen docs identity
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml python tools/scripts/check_evergreen_docs.py

lint-dogfood-reference-evidence:  ## Verify the retained self-dogfood report matches its documented digest
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml python tools/scripts/check_dogfood_reference_evidence.py

lint-public-docs-contract:  ## Verify public docs match runtime/schema/CLI capability inventories
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml python tools/scripts/check_public_docs_contract.py

test-public-docs-contract:  ## Run focused regression tests for the public docs semantic contract
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml pytest -q tools/scripts/tests/test_check_public_docs_contract.py

lint-docs: lint-evergreen-docs lint-dogfood-reference-evidence lint-public-docs-contract test-public-docs-contract  ## Verify MkDocs documentation structure, evergreen identity, and semantic capability truth
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml python tools/scripts/filter_mkdocs_warnings.py -- mkdocs build --strict
