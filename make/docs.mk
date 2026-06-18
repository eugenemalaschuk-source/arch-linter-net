.PHONY: venv docs-serve docs-build fmt-docs lint-docs

venv:  ## Create local Python virtual environment via uv
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" sync --project tools/pyproject.toml

docs-serve:  ## Start local MkDocs development server
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml mkdocs serve

docs-build:  ## Build static documentation site
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml bash -c 'mkdocs build 2>&1 | python3 tools/scripts/filter_mkdocs_warnings.py; exit $${PIPESTATUS[0]}'

fmt-docs:  ## Auto-format markdown documentation
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml mdformat docs/

lint-docs:  ## Verify MkDocs documentation structure and lint markdown
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml bash -c 'mkdocs build --strict 2>&1 | python3 tools/scripts/filter_mkdocs_warnings.py; exit $${PIPESTATUS[0]}'
