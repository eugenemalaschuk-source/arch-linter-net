.PHONY: test clean-results

test:  ## Run all tests
	@dotnet test "$(SLNX)" --no-restore

clean-results:  ## Remove test-results folder
	rm -rf "$(RESULTS_DIR)"
