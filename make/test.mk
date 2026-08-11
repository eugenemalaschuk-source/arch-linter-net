.PHONY: test test-unit test-e2e test-packed-artifact clean-results test-coverage test-coverage-main-ci test-coverage-badge _acceptance-test benchmark-cel

# Three independently addressable test buckets, single-sourced here as VSTest FullyQualifiedName
# filters. Each bucket has its own `make test-*` target (below) so unit, ordinary E2E, and the
# packed-artifact release gate can run on separate CI runners instead of contending for one.
#
# Placement rule for new fixtures:
#   - coverage-eligible unit/in-process correctness            -> unit bucket
#   - subprocess/build/filesystem/adoption integration          -> E2E bucket
#   - freshly packed/installed candidate, consumer/release proof -> packed-artifact bucket
# Adding a slow fixture to the wrong bucket is a topology regression, not a style nit.
#
# The split uses FullyQualifiedName filters, NOT [Category("E2E")]: the NUnit3TestAdapter does
# not surface fixture-level categories as VSTest traits, so `Category=E2E`/`Category!=E2E` match
# nothing. The fixtures' [Category("E2E")] attributes stay as human-readable documentation.
# `!~` negation is supported by the VSTest filter syntax.
#
# CheckpointBReleaseGateTests is its own packed-artifact bucket, not part of E2E: it packs the
# whole solution, installs the tool from an isolated feed, and builds the synthetic consumer
# fixtures across the release-evidence consumer matrix. Mixing it into ordinary E2E serialized it
# behind the rest of that bucket on the same runner, which pushed platform jobs past their budget.
TEST_E2E_FIXTURES := FullyQualifiedName~ExternalDependencyContractAuditE2eTests|FullyQualifiedName~BuildStatePreflightTests|FullyQualifiedName~BuildStatePreflightAssemblyReloadTests|FullyQualifiedName~CheckpointAAdoptionAcceptanceTests|FullyQualifiedName~ArchitectureBaselineIntegrationTests
TEST_E2E_FILTER := $(TEST_E2E_FIXTURES)
TEST_PACKED_ARTIFACT_FILTER := FullyQualifiedName~CheckpointBReleaseGateTests
TEST_UNIT_FILTER := FullyQualifiedName!~ExternalDependencyContractAuditE2eTests&FullyQualifiedName!~BuildStatePreflightTests&FullyQualifiedName!~BuildStatePreflightAssemblyReloadTests&FullyQualifiedName!~CheckpointAAdoptionAcceptanceTests&FullyQualifiedName!~ArchitectureBaselineIntegrationTests&FullyQualifiedName!~CheckpointBReleaseGateTests

test-unit:  ## Run only the coverage-eligible unit bucket
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_UNIT_FILTER)"

test-e2e:  ## Run only the ordinary E2E bucket (excludes CheckpointBReleaseGateTests)
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_E2E_FILTER)"

test-packed-artifact:  ## Run only the packed-artifact release gate (CheckpointBReleaseGateTests)
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_PACKED_ARTIFACT_FILTER)"

# `make test` remains "run the complete authoritative test set" — the union of all three buckets,
# restructured to launch as three parallel `dotnet test` processes off a single build so they never
# race on shared obj/bin output. All three are waited on regardless of any earlier one's exit
# status, then the combined result is checked explicitly — no `set -e`, which would abort the shell
# at the first failed `wait`, orphaning the remaining processes and losing their results.
test:  ## Run all tests (unit, E2E and packed-artifact buckets in parallel)
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_UNIT_FILTER)" & \
	p1=$$!; \
	dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_E2E_FILTER)" & \
	p2=$$!; \
	dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_PACKED_ARTIFACT_FILTER)" & \
	p3=$$!; \
	wait $$p1; s1=$$?; \
	wait $$p2; s2=$$?; \
	wait $$p3; s3=$$?; \
	if [ $$s1 -ne 0 ] || [ $$s2 -ne 0 ] || [ $$s3 -ne 0 ]; then exit 1; fi

# Used only by `make acceptance` (see Makefile). test and lint-architecture both build/test the
# Core.Tests project; running them concurrently races on the same obj/bin output, so acceptance
# routes test through this order-only-after-lint-architecture wrapper instead of adding that
# ordering to the public `test` target itself — standalone `make test` stays exactly "run all
# tests", with no implicit architecture-check prerequisite.
_acceptance-test: | lint-architecture
	@$(MAKE) test

clean-results:  ## Remove test-results folder
	rm -rf "$(RESULTS_DIR)"

# Coverage is coverage-only: it runs exactly the unit bucket with `--collect`. Ordinary E2E and the
# packed-artifact gate contribute no code coverage (E2E runs CLI subprocesses; the packed-artifact
# gate installs a packed tool from an isolated feed) and are no longer invoked here at all — their
# correctness signal comes from the independent `test-e2e`/`test-packed-artifact` CI jobs and from
# `make test`/`make acceptance` locally, not from the coverage/Sonar critical path. This is also why
# CheckpointBReleaseGateTests stays listed in sonar.coverage.exclusions.
test-coverage:  ## Run the unit bucket with coverage collection (Cobertura + OpenCover XML under test-results/)
	@rm -rf "$(RESULTS_DIR)"
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_UNIT_FILTER)" --logger trx --collect:"XPlat Code Coverage" \
		--results-directory "$(RESULTS_DIR)/units" \
		-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,opencover

test-coverage-main-ci:  ## Run unit-bucket coverage for main-branch badge refresh with hang diagnostics enabled
	@rm -rf "$(RESULTS_DIR)"
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_UNIT_FILTER)" --logger trx --blame-hang --blame-hang-timeout 5m \
		--collect:"XPlat Code Coverage" --results-directory "$(RESULTS_DIR)/units" \
		-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,opencover

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
