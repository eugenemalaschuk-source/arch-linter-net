.PHONY: lint lint-architecture audit-architecture lint-code-size lint-dotnet-format test-architecture-coverage-report architecture-coverage-report architecture-strict-json architecture-audit-json architecture-coverage-markdown architecture-coverage-ci

CHANGED_FILES ?= changed-files.txt
DIFF_STATUS   ?= ok

lint: lint-code-size lint-dotnet-format lint-architecture lint-docs  ## Run all code quality checks

lint-architecture:  ## Run strict architecture contracts on self
	@dotnet build "$(PROJECT_ROOT)/src/ArchLinterNet.Cli/ArchLinterNet.Cli.csproj" --nologo -v minimal
	@dotnet build "$(PROJECT_ROOT)/src/ArchLinterNet.Unity/ArchLinterNet.Unity.csproj" --nologo -v minimal
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

test-architecture-coverage-report:  ## Run tests for the architecture coverage report generator
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		pytest tools/scripts/tests/test_architecture_coverage_report.py

architecture-strict-json:  ## Run strict architecture validation, writing architecture-strict.json (target assemblies must already be built)
	@dotnet run --no-build --project "$(PROJECT_ROOT)/src/ArchLinterNet.Cli" -- \
		--policy "$(PROJECT_ROOT)/architecture/dependencies.arch.yml" --mode strict --format json \
		> "$(PROJECT_ROOT)/architecture-strict.json"

architecture-audit-json:  ## Run audit architecture validation, writing architecture-audit.json (target assemblies must already be built)
	@dotnet run --no-build --project "$(PROJECT_ROOT)/src/ArchLinterNet.Cli" -- \
		--policy "$(PROJECT_ROOT)/architecture/dependencies.arch.yml" --mode audit --format json \
		> "$(PROJECT_ROOT)/architecture-audit.json"

architecture-coverage-markdown:  ## Generate architecture-coverage.md from architecture-strict.json (CHANGED_FILES/DIFF_STATUS env optional)
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		python tools/scripts/architecture_coverage_report.py architecture-strict.json \
		--changed-files "$(CHANGED_FILES)" \
		--diff-status "$(DIFF_STATUS)" \
		--repo-root "$(PROJECT_ROOT)" \
		--output architecture-coverage.md

architecture-coverage-ci:  ## CI entrypoint: strict+audit JSON + Markdown report in one call (CHANGED_FILES/DIFF_STATUS env optional)
	@$(MAKE) architecture-strict-json; STRICT_EXIT=$$?; \
	$(MAKE) architecture-audit-json || true; \
	$(MAKE) architecture-coverage-markdown CHANGED_FILES="$(CHANGED_FILES)" DIFF_STATUS="$(DIFF_STATUS)"; MARKDOWN_EXIT=$$?; \
	if [ $$MARKDOWN_EXIT -ne 0 ]; then exit $$MARKDOWN_EXIT; fi; \
	exit $$STRICT_EXIT

architecture-coverage-report:  ## Show full-solution architecture coverage report locally (Markdown + JSON)
	@dotnet build "$(PROJECT_ROOT)/src/ArchLinterNet.Cli/ArchLinterNet.Cli.csproj" --nologo -v minimal
	@dotnet build "$(PROJECT_ROOT)/src/ArchLinterNet.Testing/ArchLinterNet.Testing.csproj" --nologo -v minimal
	@dotnet build "$(PROJECT_ROOT)/src/ArchLinterNet.Unity/ArchLinterNet.Unity.csproj" --nologo -v minimal
	@$(MAKE) architecture-strict-json
	@$(MAKE) architecture-coverage-markdown
	@echo ""
	@echo "===== Architecture coverage report (Markdown) ====="
	@cat "$(PROJECT_ROOT)/architecture-coverage.md"
	@echo ""
	@echo "===== Architecture coverage report (JSON) ====="
	@python -m json.tool < "$(PROJECT_ROOT)/architecture-strict.json"
