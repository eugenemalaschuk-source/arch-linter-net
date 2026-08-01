.PHONY: test clean-results test-coverage test-coverage-main-ci test-coverage-badge _acceptance-test benchmark-cel

# Unit tests and E2E tests run in two parallel `dotnet test` processes: the heavy E2E tests
# (real CLI subprocess builds and full-assembly analyses) no longer extend the critical path of
# the unit suite. The solution is built once up front; both processes run with --no-build so they
# never race on shared obj/bin output.
#
# The E2E split uses FullyQualifiedName filters, NOT [Category("E2E")]: the NUnit3TestAdapter does
# not surface fixture-level categories as VSTest traits, so `Category=E2E`/`Category!=E2E` match
# nothing. The fixtures' [Category("E2E")] attributes stay as human-readable documentation.
# `!~` negation is supported by the VSTest filter syntax.
TEST_E2E_FILTER := FullyQualifiedName~ExternalDependencyContractAuditE2eTests|FullyQualifiedName~BuildStatePreflightTests|FullyQualifiedName~BuildStatePreflightAssemblyReloadTests|FullyQualifiedName~CheckpointAAdoptionAcceptanceTests|FullyQualifiedName~ArchitectureBaselineIntegrationTests
TEST_UNIT_FILTER := FullyQualifiedName!~ExternalDependencyContractAuditE2eTests&FullyQualifiedName!~BuildStatePreflightTests&FullyQualifiedName!~BuildStatePreflightAssemblyReloadTests&FullyQualifiedName!~CheckpointAAdoptionAcceptanceTests&FullyQualifiedName!~ArchitectureBaselineIntegrationTests

# Both background processes are waited on regardless of the first one's exit status, then the
# combined result is checked explicitly — no `set -e`, which would abort the shell at the first
# failed `wait`, orphaning the second process and losing its result.
test:  ## Run all tests (unit tests and E2E tests in parallel)
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_UNIT_FILTER)" & \
	p1=$$!; \
	dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_E2E_FILTER)" & \
	p2=$$!; \
	wait $$p1; s1=$$?; \
	wait $$p2; s2=$$?; \
	if [ $$s1 -ne 0 ] || [ $$s2 -ne 0 ]; then exit 1; fi

# Used only by `make acceptance` (see Makefile). test and lint-architecture both build/test the
# Core.Tests project; running them concurrently races on the same obj/bin output, so acceptance
# routes test through this order-only-after-lint-architecture wrapper instead of adding that
# ordering to the public `test` target itself — standalone `make test` stays exactly "run all
# tests", with no implicit architecture-check prerequisite.
_acceptance-test: | lint-architecture
	@$(MAKE) test

clean-results:  ## Remove test-results folder
	rm -rf "$(RESULTS_DIR)"

# Coverage targets run the two suites SEQUENTIALLY, not in parallel. Microsoft.CodeCoverage
# instruments assemblies in-place in the shared bin/ output: while the units process rewrites
# (instrument at start, restore at end) those files, a concurrently running E2E process loads the
# same files — the torn reads crash its test host or surface as random BadImageFormatException
# ("Index not found") in IL-scanning tests. With one process at a time there is no rewrite/load
# overlap, so coverage is collected by the units process and the E2E process (which runs CLI
# subprocesses that never contribute to the collector anyway) runs after it without --collect.
# `make test` itself stays parallel — without coverage collection nothing rewrites bin files.
test-coverage:  ## Run all tests with coverage collection (Cobertura + OpenCover XML under test-results/)
	@rm -rf "$(RESULTS_DIR)"
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_UNIT_FILTER)" --logger trx --collect:"XPlat Code Coverage" \
		--results-directory "$(RESULTS_DIR)/units" \
		-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,opencover
	@dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_E2E_FILTER)" --logger trx \
		--results-directory "$(RESULTS_DIR)/e2e"

test-coverage-main-ci:  ## Run coverage for main-branch badge refresh with hang diagnostics enabled
	@rm -rf "$(RESULTS_DIR)"
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_UNIT_FILTER)" --logger trx --blame-hang --blame-hang-timeout 5m \
		--collect:"XPlat Code Coverage" --results-directory "$(RESULTS_DIR)/units" \
		-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,opencover
	@dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_E2E_FILTER)" --logger trx --blame-hang --blame-hang-timeout 5m \
		--results-directory "$(RESULTS_DIR)/e2e"

test-coverage-badge: test-coverage  ## Run tests with coverage and print a test-coverage badge Markdown line
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		python tools/scripts/test_coverage_badge.py --reports-glob "test-results/**/coverage.cobertura.xml"

# BENCHMARK_FILTER accepts a BenchmarkDotNet glob, e.g.
# 'make benchmark-cel BENCHMARK_FILTER=*EvaluationBenchmarks*'. Not part of `test` or `acceptance`:
# BenchmarkDotNet iterates until statistically stable, which takes minutes per class and would make
# the normal acceptance gate slow and non-deterministic in shared CI runners.
BENCHMARK_FILTER ?= *
benchmark-cel:  ## Run the ArchLinterNet.CEL BenchmarkDotNet suite (optional, not part of acceptance/test)
	@dotnet run -c Release --project "$(PROJECT_ROOT)/benchmarks/ArchLinterNet.CEL.Benchmarks" -- --filter "$(BENCHMARK_FILTER)"
