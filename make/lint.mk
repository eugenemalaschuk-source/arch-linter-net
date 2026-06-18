.PHONY: lint lint-architecture audit-architecture lint-code-size lint-dotnet-format

lint: lint-code-size lint-dotnet-format lint-architecture  ## Run all code quality checks

lint-architecture:  ## Run strict architecture contracts on self
	@dotnet test "$(TESTS_DIR)/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj" --no-restore

audit-architecture:  ## Run diagnostic architecture audit contracts
	@dotnet run --project "$(PROJECT_ROOT)/src/ArchLinterNet.Cli" -- --policy "$(PROJECT_ROOT)/architecture/dependencies.arch.yml" --mode audit

lint-code-size:  ## Size lint for C# and documentation files
	@cd "$(TOOLS_DIR)" && "$(UV)" run python scripts/lint_csharp_file_size.py \
		--warn-lines "$(CS_SIZE_LINT_WARN_LINES)" \
		--error-lines "$(CS_SIZE_LINT_ERROR_LINES)" \
		$(CS_SIZE_LINT_ROOTS:%=../%)

lint-dotnet-format:  ## Verify C# formatting without changing files
	@dotnet format "$(SLNX)" --verify-no-changes --verbosity minimal
