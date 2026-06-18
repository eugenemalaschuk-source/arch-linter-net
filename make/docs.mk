.PHONY: venv docs-serve docs-build fmt-docs lint-docs

venv:  ## Create local Python virtual environment via uv
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" sync --project tools/pyproject.toml

docs-serve:  ## Start local MkDocs development server
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml mkdocs serve

docs-build:  ## Build static documentation site
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml mkdocs build

fmt-docs:  ## Auto-format markdown documentation
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml mdformat docs/

lint-docs:  ## Verify MkDocs documentation structure and lint markdown
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml mkdocs build --strict
