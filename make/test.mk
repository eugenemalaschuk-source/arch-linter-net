.PHONY: test clean-results test-coverage test-coverage-badge _acceptance-test

test:  ## Run all tests
	@dotnet test "$(SLNX)" --no-restore

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
	@dotnet test "$(SLNX)" --no-restore --logger trx --collect:"XPlat Code Coverage" --results-directory "$(RESULTS_DIR)" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,opencover

test-coverage-badge: test-coverage  ## Run tests with coverage and print a test-coverage badge Markdown line
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		python tools/scripts/test_coverage_badge.py --reports-glob "test-results/**/coverage.cobertura.xml"
