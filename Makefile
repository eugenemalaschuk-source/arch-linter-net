## ArchLinterNet — local developer targets
##
## Bootstrap & environment:
##   make bundle               — install development tools for the current OS
##   make rtk-init             — install/configure RTK without telemetry
##   make restore              — restore NuGet packages for all .NET projects
##
## Formatting:
##   make fmt                  — auto-format all C# code
##
## Linting & quality:
##   make lint                 — run all code quality checks
##   make lint-architecture    — run strict architecture contracts on self
##   make audit-architecture   — run diagnostic architecture audit on self
##   make lint-code-size       — size lint for C# and documentation files
##   make lint-dotnet-format   — verify C# formatting without changing files
##
## Testing:
##   make verify               — lint + all tests
##   make test                 — run all tests
##
## Documentation:
##   make venv                 — create local Python virtual environment via uv
##   make docs-serve           — start local MkDocs development server
##   make docs-build           — build static documentation site
##
## Utilities:
##   make clean-results        — remove test-results folder

include make/paths.mk
include make/dev.mk
include make/lint.mk
include make/test.mk

.DEFAULT_GOAL := help
.PHONY: help

help:
	@awk '/^## / { sub(/^## /, "", $$0); print }' $(MAKEFILE_LIST)

venv:
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" sync --project tools/pyproject.toml

docs-serve:
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml mkdocs serve

docs-build:
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml mkdocs build
