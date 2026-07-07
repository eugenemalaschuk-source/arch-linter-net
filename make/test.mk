.PHONY: test clean-results test-coverage test-coverage-badge

# Order-only: test and lint-architecture both build/test the Core.Tests project; running them
# concurrently races on the same obj/bin output. lint-code-size, lint-docs, and lint-dotnet-format
# don't touch dotnet build output and are safe to run alongside test.
test: | lint-architecture  ## Run all tests
	@dotnet test "$(SLNX)" --no-restore

clean-results:  ## Remove test-results folder
	rm -rf "$(RESULTS_DIR)"

test-coverage:  ## Run all tests with coverage collection (cobertura XML under test-results/)
	@rm -rf "$(RESULTS_DIR)"
	@dotnet test "$(SLNX)" --no-restore --collect:"XPlat Code Coverage" --results-directory "$(RESULTS_DIR)"

test-coverage-badge: test-coverage  ## Run tests with coverage and print a test-coverage badge Markdown line
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		python tools/scripts/test_coverage_badge.py --reports-glob "test-results/**/coverage.cobertura.xml"
