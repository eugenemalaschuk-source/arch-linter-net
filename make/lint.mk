.PHONY: lint _lint-dotnet lint-architecture audit-architecture policy-check explain-architecture public-api-check public-api-update-preview public-api-update lint-code-size lint-dotnet-format lint-workflows fmt-workflows test-release-evidence test-calculate-version test-coverage-badge-script test-tooling-coverage architecture-coverage-report architecture-strict-json architecture-audit-json architecture-coverage-markdown architecture-coverage-comment-markdown architecture-coverage-ci

CHANGED_FILES ?= changed-files.txt
DIFF_STATUS   ?= ok

# dotnet format, architecture self-validation, and shard-membership discovery all touch the normal
# repository build graph. Running them as independent prerequisites under `make -j` can make two
# MSBuild processes write the same bin/obj files concurrently (for example ArchLinterNet.CEL.deps.json).
# Keep only these build-output-mutating checks serialized; code-size and docs lint remain free to run
# in parallel with this chain.
_lint-dotnet:
	@$(MAKE) lint-dotnet-format
	@$(MAKE) lint-architecture
	@$(MAKE) lint-test-shard-membership

lint: lint-code-size lint-docs _lint-dotnet  ## Run all code quality checks

# `make acceptance` starts `lint` and `_acceptance-test` in the same parallel make invocation.
# make/test.mk already makes the test phase wait for lint-architecture, but that is not sufficient:
# after lint-architecture finishes, shard-membership discovery could still build Core.Tests while
# `_acceptance-test` starts the full solution build. Sharing this order-only prerequisite makes the
# test phase wait until every build-output-mutating lint command above has finished, without waiting
# for independent docs/code-size lint.
_acceptance-test: | _lint-dotnet

# The single authoritative definition of "the repository satisfies its own architecture policy".
# It is read-only with respect to the policy and the reviewed API snapshots: --ensure-built prepares
# and verifies the project graph (replacing the explicit `dotnet build` calls this target used to
# make), but nothing under architecture/ is ever rewritten. `SelfArchitecturePolicyTests` runs the
# same policy through the ArchLinterNet.Testing adapter as parity evidence inside `make test`; it is
# not a second definition of success.
lint-architecture:  ## Canonical read-only strict self-policy gate (builds and verifies the project graph)
	@dotnet build "$(SLNX)" --nologo --no-restore -m:1
	@dotnet run --no-build --project "$(CLI_PROJECT)" -- \
		--policy "$(POLICY)" --mode strict --ensure-built

audit-architecture:  ## Run diagnostic architecture audit contracts
	@dotnet run --project "$(CLI_PROJECT)" -- \
		--policy "$(POLICY)" --mode audit --ensure-built

policy-check:  ## Fast policy-only validation: schema, imports, composition (no project or assembly analysis)
	@dotnet run --project "$(CLI_PROJECT)" -- policy check --policy "$(POLICY)"

explain-architecture:  ## Explain why SOURCE reaches TARGET under the self-policy (SOURCE=<id> TARGET=<id>)
	@if [ -z "$(SOURCE)" ] || [ -z "$(TARGET)" ]; then \
		echo "usage: make explain-architecture SOURCE=<layer-or-type> TARGET=<layer-or-type>"; \
		exit 2; \
	fi
	@dotnet run --project "$(CLI_PROJECT)" -- explain \
		--policy "$(POLICY)" --source "$(SOURCE)" --target "$(TARGET)"

# ── Reviewed public API lifecycle ───────────────────────────────────────────
# check → read-only drift detection (what lint/CI use), update-preview → dry-run of the rewrite,
# update → the explicit, human-initiated snapshot rewrite. Only the last one writes.
public-api-check:  ## Read-only diff of every reviewed public API snapshot against the live surface
	@for surface in $(PUBLIC_API_SURFACES); do \
		contract="$${surface%%=*}"; snapshot="$${surface#*=}"; \
		echo "public-api diff: $$contract"; \
		dotnet run --project "$(CLI_PROJECT)" -- public-api diff \
			--policy "$(POLICY)" --contract "$$contract" --snapshot "$$snapshot" --ensure-built || exit $$?; \
	done

public-api-update-preview:  ## Preview the snapshot rewrite for every reviewed public API surface (writes nothing)
	@for surface in $(PUBLIC_API_SURFACES); do \
		contract="$${surface%%=*}"; snapshot="$${surface#*=}"; \
		echo "public-api update --dry-run: $$contract"; \
		dotnet run --project "$(CLI_PROJECT)" -- public-api update \
			--policy "$(POLICY)" --contract "$$contract" --snapshot "$$snapshot" --dry-run --ensure-built || exit $$?; \
	done

public-api-update:  ## Rewrite every reviewed public API snapshot from the live surface (explicit review action)
	@for surface in $(PUBLIC_API_SURFACES); do \
		contract="$${surface%%=*}"; snapshot="$${surface#*=}"; \
		echo "public-api update: $$contract"; \
		dotnet run --project "$(CLI_PROJECT)" -- public-api update \
			--policy "$(POLICY)" --contract "$$contract" --snapshot "$$snapshot" --ensure-built || exit $$?; \
	done

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

test-release-evidence:  ## Run tests for the packed-artifact release-evidence aggregator
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		pytest tools/release/tests

test-calculate-version:  ## Run tests for the release version-calculation script
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		pytest tools/scripts/tests/test_calculate_version.py

test-coverage-badge-script:  ## Run tests for the test-coverage badge Markdown generator
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		pytest tools/scripts/tests/test_test_coverage_badge.py

# Both Python suites in one run, emitting the Cobertura report SonarCloud needs. Without it the
# release-evidence aggregator and the coverage-report generator are measured as 0%-covered new
# code even though both are tested.
test-tooling-coverage:  ## Run all Python tooling tests with coverage (coverage-python.xml)
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		pytest tools/release/tests \
		tools/scripts/tests/test_calculate_version.py \
		tools/scripts/tests/test_check_dogfood_reference_evidence.py \
		tools/scripts/tests/test_check_evergreen_docs.py \
		tools/scripts/tests/test_check_evergreen_docs_edges.py \
		tools/scripts/tests/test_test_coverage_badge.py \
		tools/scripts/tests/test_verify_core_unit_shards.py \
		--cov=tools/release --cov=tools/scripts \
		--cov-report=xml:coverage-python.xml --cov-report=term-missing

# --ensure-built prepares and verifies the analysed project graph; the policy declares
# analysis.solution, so a run without a build receipt is blocked by build-state preflight. It never
# writes to architecture/. --no-build applies to the CLI host itself, which the caller builds.
architecture-strict-json:  ## Run strict+audit validation once, writing combined JSON (CLI must already be built)
	@dotnet run --no-build --project "$(CLI_PROJECT)" -- \
		--policy "$(POLICY)" --mode strict,audit --ensure-built --format json \
		> "$(PROJECT_ROOT)/architecture-results.json" || true
	@dotnet run --no-build --project "$(CLI_PROJECT)" -- coverage extract --input architecture-results.json --mode strict --output architecture-strict.json
	@dotnet run --no-build --project "$(CLI_PROJECT)" -- badge architecture-policy --input architecture-strict.json > /dev/null

architecture-audit-json:  ## Materialize the shared strict+audit JSON under the audit artifact name
	@dotnet run --no-build --project "$(CLI_PROJECT)" -- coverage extract --input architecture-results.json --mode audit --output architecture-audit.json

architecture-coverage-markdown:  ## Generate architecture-coverage.md from architecture-strict.json (CHANGED_FILES/DIFF_STATUS env optional)
	@dotnet run --no-build --project "$(CLI_PROJECT)" -- coverage report \
		--input architecture-strict.json --changed-files "$(CHANGED_FILES)" --diff-status "$(DIFF_STATUS)" \
		--repo-root "$(PROJECT_ROOT)" --output architecture-coverage.md

architecture-coverage-comment-markdown:  ## Generate compact architecture-coverage-comment.md for a PR comment
	@dotnet run --no-build --project "$(CLI_PROJECT)" -- coverage report \
		--input architecture-strict.json --changed-files "$(CHANGED_FILES)" --diff-status "$(DIFF_STATUS)" \
		--repo-root "$(PROJECT_ROOT)" --max-failure-diagnostics 3 --output architecture-coverage-comment.md

architecture-coverage-ci:  ## CI entrypoint: strict+audit JSON + Markdown report in one call (CHANGED_FILES/DIFF_STATUS env optional)
	@$(MAKE) architecture-strict-json || true; \
	$(MAKE) architecture-audit-json || true; \
	$(MAKE) architecture-coverage-markdown CHANGED_FILES="$(CHANGED_FILES)" DIFF_STATUS="$(DIFF_STATUS)"; MARKDOWN_EXIT=$$?; \
	$(MAKE) architecture-coverage-comment-markdown CHANGED_FILES="$(CHANGED_FILES)" DIFF_STATUS="$(DIFF_STATUS)"; COMMENT_EXIT=$$?; \
	dotnet run --no-build --project "$(CLI_PROJECT)" -- badge architecture-policy --input architecture-strict.json > /dev/null; STRICT_EXIT=$$?; \
	if [ $$MARKDOWN_EXIT -ne 0 ]; then exit $$MARKDOWN_EXIT; fi; \
	if [ $$COMMENT_EXIT -ne 0 ]; then exit $$COMMENT_EXIT; fi; \
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
