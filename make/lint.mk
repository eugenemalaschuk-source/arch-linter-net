.PHONY: lint lint-architecture audit-architecture lint-code-size lint-dotnet-format

lint: lint-code-size lint-dotnet-format lint-architecture lint-docs  ## Run all code quality checks

lint-architecture:  ## Run strict architecture contracts on self
	@dotnet build "$(PROJECT_ROOT)/src/ArchLinterNet.Cli/ArchLinterNet.Cli.csproj" --nologo -v minimal
	@dotnet build "$(PROJECT_ROOT)/src/ArchLinterNet.Unity/ArchLinterNet.Unity.csproj" --nologo -v minimal; true
	@dotnet test "$(TESTS_DIR)/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj" --no-restore

audit-architecture:  ## Run diagnostic architecture audit contracts
	@dotnet build "$(PROJECT_ROOT)/src/ArchLinterNet.Testing/ArchLinterNet.Testing.csproj" --nologo -q 2>/dev/null
	@dotnet build "$(PROJECT_ROOT)/src/ArchLinterNet.Unity/ArchLinterNet.Unity.csproj" --nologo -q 2>/dev/null; true
	@dotnet run --project "$(PROJECT_ROOT)/src/ArchLinterNet.Cli" -- --policy "$(PROJECT_ROOT)/architecture/dependencies.arch.yml" --mode audit

lint-code-size:  ## Size lint for C# and documentation files
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		python tools/scripts/lint_csharp_file_size.py \
		--warn-lines "$(CS_SIZE_LINT_WARN_LINES)" \
		--error-lines "$(CS_SIZE_LINT_ERROR_LINES)" \
		$(CS_SIZE_LINT_ROOTS)

lint-dotnet-format:  ## Verify C# formatting without changing files
	@dotnet format "$(SLNX)" --verify-no-changes --verbosity minimal
