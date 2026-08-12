.PHONY: lint lint-architecture audit-architecture lint-code-size lint-dotnet-format lint-workflows fmt-workflows test-architecture-coverage-report test-release-evidence test-tooling-coverage architecture-coverage-report architecture-strict-json architecture-audit-json architecture-coverage-markdown architecture-coverage-ci

CHANGED_FILES ?= changed-files.txt
DIFF_STATUS   ?= ok

lint: lint-code-size lint-dotnet-format lint-architecture lint-docs lint-test-shard-membership  ## Run all code quality checks

lint-architecture:  ## Run strict architecture contracts on self
	@dotnet build "$(PROJECT_ROOT)/src/ArchLinterNet.Cli/ArchLinterNet.Cli.csproj" --nologo -v minimal
	@dotnet build "$(PROJECT_ROOT)/src/ArchLinterNet.Testing/ArchLinterNet.Testing.csproj" --nologo -v minimal
	@dotnet test "$(TESTS_DIR)/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj" --no-restore \
		--filter "FullyQualifiedName=ArchLinterNet.Core.Tests.SelfArchitecturePolicyTests.RepositoryPolicy_ValidatesOwnInternalBoundaries"

audit-architecture:  ## Run diagnostic architecture audit contracts
	@dotnet build "$(PROJECT_ROOT)/src/ArchLinterNet.Testing/ArchLinterNet.Testing.csproj" --nologo -q 2>/dev/null
	@dotnet run --project "$(PROJECT_ROOT)/src/ArchLinterNet.Cli" -- --policy "$(PROJECT_ROOT)/architecture/dependencies.arch.yml" --mode audit

lint-code-size:  ## Size lint for C# and documentation files
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		python tools/scripts/lint_csharp_file_size.py \
		--warn-lines "$(CS_SIZE_LINT_WARN_LINES)" \
		--error-lines "$(CS_SIZE_LINT_ERROR_LINES)" \
		$(CS_SIZE_LINT_ROOTS)

lint-dotnet-format:  ## Verify C# formatting without changing files
	@dotnet format "$(SLNX)" --verify-no-changes --verbosity minimal

lint-workflows:  ## Lint GitHub Actions workflows (actionlint + zizmor + prettier --check)
	@printf '\033[1;33m══════════════════════════════════════════\n  🔍  actionlint: .github/workflows/\n══════════════════════════════════════════\033[0m\n'
	@command -v actionlint >/dev/null 2>&1 || ( \
		echo "actionlint is not installed or is not on PATH. Run 'make bundle' to install workflow tooling."; \
		exit 1 \
	)
	@actionlint .github/workflows/*.yml
	@printf '\033[1;33m══════════════════════════════════════════\n  🔐  zizmor: .github/workflows/\n══════════════════════════════════════════\033[0m\n'
	@command -v zizmor >/dev/null 2>&1 || ( \
		echo "zizmor is not installed or is not on PATH. Run 'make bundle' to install workflow tooling."; \
		exit 1 \
	)
	@zizmor --min-severity low .github/workflows/*.yml
	@printf '\033[1;33m══════════════════════════════════════════\n  🎨  prettier --check: .github/workflows/\n══════════════════════════════════════════\033[0m\n'
	@npx --yes prettier --check ".github/workflows/*.yml"

fmt-workflows:  ## Format GitHub Actions workflows with prettier
	@printf '\033[1;36m══════════════════════════════════════════\n  🎨  prettier --write: .github/workflows/\n══════════════════════════════════════════\033[0m\n'
	@npx --yes prettier --write ".github/workflows/*.yml"

test-architecture-coverage-report:  ## Run tests for the architecture coverage report generator
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		pytest tools/scripts/tests/test_architecture_coverage_report.py

test-release-evidence:  ## Run tests for the packed-artifact release-evidence aggregator
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		pytest tools/release/tests

# Both Python suites in one run, emitting the Cobertura report SonarCloud needs. Without it the
# release-evidence aggregator and the coverage-report generator are measured as 0%-covered new
# code even though both are tested.
test-tooling-coverage:  ## Run all Python tooling tests with coverage (coverage-python.xml)
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		pytest tools/release/tests \
		tools/scripts/tests/test_architecture_coverage_report.py \
		tools/scripts/tests/test_verify_core_unit_shards.py \
		--cov=tools/release --cov=tools/scripts \
		--cov-report=xml:coverage-python.xml --cov-report=term-missing

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
	@$(MAKE) architecture-strict-json
	@$(MAKE) architecture-coverage-markdown
	@echo ""
	@echo "===== Architecture coverage report (Markdown) ====="
	@cat "$(PROJECT_ROOT)/architecture-coverage.md"
	@echo ""
	@echo "===== Architecture coverage report (JSON) ====="
	@python -m json.tool < "$(PROJECT_ROOT)/architecture-strict.json"
