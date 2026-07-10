## ArchLinterNet — local developer targets
##
## Bootstrap & environment:
##   make setup                — full project bootstrap: bundle + restore + venv
##   make bundle               — install development tools for the current OS
##   make rtk-init             — install/configure RTK without telemetry
##   make restore              — restore NuGet packages for all .NET projects
##   make venv                 — create local Python virtual environment via uv
##
## Formatting:
##   make fmt                  — auto-format all code and documentation
##   make fmt-csharp           — auto-format all C# code
##   make fmt-docs             — auto-format markdown documentation
##
## Linting & quality:
##   make lint                              — run all code quality checks
##   make lint-architecture                 — run strict architecture contracts on self
##   make audit-architecture                — run diagnostic architecture audit on self
##   make lint-code-size                    — size lint for C# and documentation files
##   make lint-dotnet-format                — verify C# formatting without changing files
##   make lint-docs                         — verify MkDocs documentation structure
##   make architecture-coverage-report      — show full-solution coverage report locally (Markdown + JSON)
##   make test-architecture-coverage-report — run tests for the coverage report generator
##
## Testing:
##   make acceptance           — lint + all tests
##   make test                 — run all tests
##   make test-coverage        — run all tests with coverage collection (cobertura XML)
##   make test-coverage-badge  — run tests with coverage and print a test-coverage badge line
##
## Build:
##   make build                — build documentation site + NuGet packages
##   make docs-serve           — start local MkDocs development server
##   make docs-build           — build static documentation site
##   make pack                 — build NuGet packages
##
## Utilities:
##   make clean-results        — remove test-results folder

include make/paths.mk
include make/dev.mk
include make/docs.mk
include make/lint.mk
include make/test.mk

.DEFAULT_GOAL := help
.PHONY: help

help:
	@awk '/^## / { sub(/^## /, "", $$0); print }' $(MAKEFILE_LIST)

setup: bundle restore venv  ## Full project bootstrap: tools + NuGet + Python venv

fmt: fmt-csharp fmt-docs  ## Auto-format all code and documentation

build: docs-build pack  ## Build documentation site and NuGet packages

acceptance:  ## Full project acceptance: lint + all tests (runs independent checks in parallel)
	@echo "acceptance: running with NPROC=$(NPROC) (override with 'make acceptance NPROC=1' to force serial)"
	@$(MAKE) -j$(NPROC) lint _acceptance-test COVERAGE=$(COVERAGE)
