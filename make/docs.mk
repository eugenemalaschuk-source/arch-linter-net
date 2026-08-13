.PHONY: venv docs-serve docs-build fmt-docs lint-docs lint-evergreen-docs

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

lint-docs: lint-evergreen-docs  ## Verify MkDocs documentation structure and evergreen identity policy
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml python tools/scripts/filter_mkdocs_warnings.py -- mkdocs build --strict
