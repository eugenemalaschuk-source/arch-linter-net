.PHONY: test clean-results test-coverage test-coverage-main-ci test-coverage-badge _acceptance-test benchmark-cel

# Unit tests and E2E tests run in two parallel `dotnet test` processes: the heavy E2E tests
# ([Category("E2E")] — real CLI subprocess builds and full-assembly analyses) no longer extend the
# critical path of the unit suite. The solution is built once up front; both processes run with
# --no-build so they never race on shared obj/bin output.
test:  ## Run all tests (unit tests and E2E tests in parallel)
	@dotnet build "$(SLNX)" --no-restore --nologo
	@set -e; \
	dotnet test "$(SLNX)" --no-restore --no-build --filter "Category!=E2E" & \
	p1=$$!; \
	dotnet test "$(SLNX)" --no-restore --no-build --filter "Category=E2E" & \
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

test-coverage:  ## Run all tests with coverage collection (Cobertura + OpenCover XML under test-results/)
	@rm -rf "$(RESULTS_DIR)"
	@dotnet build "$(SLNX)" --no-restore --nologo
	@set -e; \
	dotnet test "$(SLNX)" --no-restore --no-build --logger trx --collect:"XPlat Code Coverage" \
		--results-directory "$(RESULTS_DIR)/units" \
		-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,opencover & \
	p1=$$!; \
	dotnet test "$(SLNX)" --no-restore --no-build --filter "Category=E2E" --logger trx --collect:"XPlat Code Coverage" \
		--results-directory "$(RESULTS_DIR)/e2e" \
		-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,opencover & \
	p2=$$!; \
	wait $$p1; s1=$$?; \
	wait $$p2; s2=$$?; \
	if [ $$s1 -ne 0 ] || [ $$s2 -ne 0 ]; then exit 1; fi

test-coverage-main-ci:  ## Run coverage for main-branch badge refresh with hang diagnostics enabled
	@rm -rf "$(RESULTS_DIR)"
	@dotnet build "$(SLNX)" --no-restore --nologo
	@set -e; \
	dotnet test "$(SLNX)" --no-restore --no-build --logger trx --blame-hang --blame-hang-timeout 5m \
		--collect:"XPlat Code Coverage" --results-directory "$(RESULTS_DIR)/units" \
		-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,opencover & \
	p1=$$!; \
	dotnet test "$(SLNX)" --no-restore --no-build --filter "Category=E2E" --logger trx --blame-hang --blame-hang-timeout 5m \
		--collect:"XPlat Code Coverage" --results-directory "$(RESULTS_DIR)/e2e" \
		-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,opencover & \
	p2=$$!; \
	wait $$p1; s1=$$?; \
	wait $$p2; s2=$$?; \
	if [ $$s1 -ne 0 ] || [ $$s2 -ne 0 ]; then exit 1; fi

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
