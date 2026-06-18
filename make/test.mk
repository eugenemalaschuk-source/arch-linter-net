.PHONY: verify test clean-results

verify: lint test  ## Standard local verification: lint + all tests

test:  ## Run all tests
	@dotnet test "$(SLNX)" --no-restore

clean-results:  ## Remove test-results folder
	rm -rf "$(RESULTS_DIR)"
