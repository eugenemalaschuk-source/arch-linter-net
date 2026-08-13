## ArchLinterNet — local developer targets
##
## Bootstrap & environment:
##   make setup                — full project bootstrap: bundle + restore + venv
##   make bundle               — install development tools for the current OS
##   make restore              — restore NuGet packages for all .NET projects
##   make venv                 — create local Python virtual environment via uv
##
## Formatting:
##   make fmt                  — auto-format all code and documentation
##   make fmt-csharp           — auto-format all C# code
##   make fmt-docs             — auto-format markdown documentation
##   make fmt-workflows        — format GitHub Actions workflows with prettier
##
## Linting & quality:
##   make lint                              — run all code quality checks
##   make lint-architecture                 — canonical read-only strict self-policy gate
##   make audit-architecture                — run diagnostic architecture audit on self
##   make policy-check                      — fast policy-only validation (no project/assembly analysis)
##   make public-api-check                  — read-only reviewed public API drift check
##   make public-api-update-preview         — preview the reviewed public API snapshot rewrite
##   make public-api-update                 — rewrite reviewed public API snapshots (explicit action)
##   make explain-architecture SOURCE=.. TARGET=..  — explain one dependency edge under the self-policy
##   make lint-code-size                    — size lint for C# and documentation files
##   make lint-dotnet-format                — verify C# formatting without changing files
##   make lint-docs                         — verify MkDocs documentation structure
##   make lint-workflows                    — lint GitHub Actions workflows
##   make lint-test-shard-membership        — verify Core unit shard tokens are live and leak-free
##   make architecture-coverage-report      — show full-solution coverage report locally (Markdown + JSON)
##   make test-architecture-coverage-report — run tests for the coverage report generator
##
## Testing:
##   make acceptance           — lint + all tests
##   make test                 — run all tests (unit + E2E + packed-artifact buckets)
##   make test-unit            — run the complete coverage-eligible unit bucket (both Core shards plus CEL/Cli)
##   make test-unit-core-1     — run only Core unit shard 1 (heaviest fixture classes)
##   make test-unit-core-2     — run only Core unit shard 2 (remainder)
##   make test-unit-other      — run the unit bucket's non-Core assemblies (CEL.Tests, Cli.Tests)
##   make test-e2e             — run only the ordinary E2E bucket (excludes CheckpointBReleaseGateTests)
##   make test-packed-artifact — run only the packed-artifact release gate (CheckpointBReleaseGateTests)
##   make test-coverage        — run the unit bucket with coverage collection (cobertura XML)
##   make test-coverage-badge  — run tests with coverage and print a test-coverage badge line
##   make test-release-evidence — run tests for the packed-artifact release-evidence aggregator
##   make test-calculate-version — run tests for the release version-calculation script
##   make test-coverage-badge-script — run tests for the test-coverage badge Markdown generator
##   make test-tooling-coverage — run all Python tooling tests with coverage
##   make benchmark-cel        — run the ArchLinterNet.CEL BenchmarkDotNet suite (optional, not part of acceptance)
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

fmt: fmt-csharp fmt-docs fmt-workflows  ## Auto-format all code and documentation

build: docs-build pack  ## Build documentation site and NuGet packages

acceptance:  ## Full project acceptance: lint + all tests (runs independent checks in parallel)
	@echo "acceptance: running with NPROC=$(NPROC) (override with 'make acceptance NPROC=1' to force serial)"
	@$(MAKE) -j$(NPROC) lint _acceptance-test
