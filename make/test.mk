.PHONY: test test-unit test-unit-core-1 test-unit-core-2 test-unit-other test-e2e test-packed-artifact clean-results test-coverage test-coverage-main-ci test-coverage-badge _acceptance-test benchmark-cel lint-test-shard-membership

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
#
# Control characters in [TestCase(...)] arguments silently vanish from every bucket above: NUnit
# renders non-printable argument characters into a display name using backslash-escape sequences
# (e.g. an ESC byte becomes the six characters backslash-u-0-0-1-b), and that display name
# becomes the test case's FullyQualifiedName. The NUnit3TestAdapter's fallback filter parser
# (used whenever a filter mixes `&`/`!~`, as all the filters below do) re-parses
# FullyQualifiedName through the same backslash-escape grammar used
# for filter expression text; a backslash not followed by one of that grammar's recognized escape
# targets (`\(){}&|=!~`) throws there, and the affected case is excluded from every
# FullyQualifiedName filter bucket with no diagnostic (see issue #480, and
# ReportCoordinatorTests.TerminatorsC1SequencesAndMalformedInputCases for the fix pattern). Any new
# parameterized fixture whose arguments embed raw control bytes must give each case an explicit,
# ASCII, backslash-free name via `TestCaseData(...).SetName(...)` instead of a bare
# `[TestCase(...)]` — the arguments passed to the test body are unaffected, only the generated
# identity used for filtering and reporting.
TEST_E2E_FIXTURES := FullyQualifiedName~ExternalDependencyContractAuditE2eTests|FullyQualifiedName~BuildStatePreflightTests|FullyQualifiedName~BuildStatePreflightAssemblyReloadTests|FullyQualifiedName~CheckpointAAdoptionAcceptanceTests|FullyQualifiedName~ArchitectureBaselineIntegrationTests
TEST_E2E_FILTER := $(TEST_E2E_FIXTURES)
TEST_PACKED_ARTIFACT_FILTER := FullyQualifiedName~CheckpointBReleaseGateTests
TEST_UNIT_FILTER := FullyQualifiedName!~ExternalDependencyContractAuditE2eTests&FullyQualifiedName!~BuildStatePreflightTests&FullyQualifiedName!~BuildStatePreflightAssemblyReloadTests&FullyQualifiedName!~CheckpointAAdoptionAcceptanceTests&FullyQualifiedName!~ArchitectureBaselineIntegrationTests&FullyQualifiedName!~CheckpointBReleaseGateTests

# ArchLinterNet.Core.Tests carries no [assembly: Parallelizable] (unlike ArchLinterNet.Cli.Tests),
# runs strictly serially, and is the dominant cost inside the unit bucket (~2600 of the bucket's
# ~3600 tests, and effectively all of its wall-clock — see
# docs/internal/core-unit-shard-inventory.md for the measured baseline). It is split into two
# deterministic shards so unit_tests can run them as independent CI matrix legs instead of one
# monolithic per-platform job.
#
# Shard 1 is a pure-OR list of ~54 fixture classes. It starts from the classes measured (or, for
# the defensively-included Checkers.* group, suspected) to be individually heaviest — Roslyn/IL
# method-body resolution, project/framework-reference resolution, filesystem/build-preservation,
# and reflection-heavy checkers — then adds enough further classes, picked by test *count* rather
# than measured per-test duration, to bring shard 1 to roughly half of the bucket's ~2571 tests.
# This two-step selection matters: an earlier duration-only split (the ~16 heaviest classes alone,
# ~5% of the bucket by count) measured as balanced by summed per-test TRX duration but was NOT
# balanced in practice — with ~2400 tests left in the remainder shard, per-test/per-process
# framework overhead invisible to any single test's recorded duration dominated its real
# wall-clock. See docs/internal/core-unit-shard-inventory.md for the measured evidence.
#
# Shard 2 is the remainder: TEST_UNIT_FILTER with one additional `!~` negation per shard-1 token,
# so a fixture nobody explicitly assigns is fail-closed into shard 2 rather than silently
# unassigned. tools/scripts/verify_core_unit_shards.py mechanically checks that no shard-1 token
# is dead (matches nothing) or leaks into the E2E/packed-artifact buckets — see
# lint-test-shard-membership below.
#
# Both shard filters are scoped to the ArchLinterNet.Core.Tests project specifically (see
# CORE_TESTS_CSPROJ below), not the .slnx: TEST_UNIT_FILTER's negations are class-name substrings
# that don't exist in ArchLinterNet.CEL.Tests/ArchLinterNet.Cli.Tests, so those assemblies would
# pass every negation vacuously and run in BOTH shards if a shard filter were applied against the
# whole solution. test-unit-other runs those two (already-fast, already internally parallel)
# assemblies once, on their own.
TEST_CORE_UNIT_SHARD_1_FIXTURES := FullyQualifiedName~PerTestDurationGuardAttributeTests|FullyQualifiedName~EnsureBuiltNonDestructiveIntegrationTests|FullyQualifiedName~FrameworkReferenceContractTests|FullyQualifiedName~FrameworkReferenceConfigurationTests|FullyQualifiedName~FrameworkReferenceBaselineIdentityTests|FullyQualifiedName~ArchitectureAnalysisSessionMethodBodyProjectAwareTests|FullyQualifiedName~FrameworkReferenceAllowOnlyContractTests|FullyQualifiedName~ArchitectureProjectRoslynContextResolverTests|FullyQualifiedName~AspNetSharedFrameworkAcceptanceTests|FullyQualifiedName~BoundedParallelPartitionRunnerTests|FullyQualifiedName~CompositionContractTests|FullyQualifiedName~CelBoundaryArchitectureTests|FullyQualifiedName~PublicApiSurfaceCheckerTests|FullyQualifiedName~InheritanceCheckerTests|FullyQualifiedName~AssemblyIndependenceCheckerTests|FullyQualifiedName~AcyclicSiblingContractTests|FullyQualifiedName~ArchitectureContractSchemaInstanceValidationTests|FullyQualifiedName~ArchitecturePolicyImportTests|FullyQualifiedName~ArchitectureDiagnosticFormatterTests|FullyQualifiedName~ExpressionCompilationValidatorTests|FullyQualifiedName~LayerResolverGlobTests|FullyQualifiedName~ArchitectureSourceFileFactIndexTests|FullyQualifiedName~ArchitectureSarifFormatterTests|FullyQualifiedName~AnalysisCacheStoreTests|FullyQualifiedName~ArchitectureRoleIndexTests|FullyQualifiedName~LayerResolverTests|FullyQualifiedName~ArchitectureAttributeRoleExtractorTests|FullyQualifiedName~LayoutConventionContractTests|FullyQualifiedName~AttributeUsageContractTests|FullyQualifiedName~ArchitecturePublicApiSignatureDetailsCoverageTests|FullyQualifiedName~TypePlacementContractTests|FullyQualifiedName~ContextualContractSchemaTests|FullyQualifiedName~ArchitectureContractSchemaTests|FullyQualifiedName~ArchitectureBaselineApplicationServiceFakeCompositionTests|FullyQualifiedName~ArchitectureCoverageSummaryTests|FullyQualifiedName~ArchitecturePublicApiApplicationServiceTests|FullyQualifiedName~BaselineSafeAuthoringTests|FullyQualifiedName~ArchitecturePolicyProvenanceTests|FullyQualifiedName~PolicyConsistencyCheckTests|FullyQualifiedName~CelSelectorContextualIntegrationTests|FullyQualifiedName~InterfaceImplementationContractTests|FullyQualifiedName~RuleInputCoverageValidationTests|FullyQualifiedName~ContextualContractValidationTests|FullyQualifiedName~ArchitecturePolicyEffectiveSchemaValidatorComposedTests|FullyQualifiedName~EvaluatedBuildInputManifestTests|FullyQualifiedName~PublicApiSurfaceContractTests|FullyQualifiedName~AnalysisCacheDiagnosticPayloadConverterTests|FullyQualifiedName~ArchitectureContractHandlerRegistryTests|FullyQualifiedName~ArchitectureDeclaredTypeParserTests|FullyQualifiedName~ArchitectureValidatorTests|FullyQualifiedName~TestingAdapterTests|FullyQualifiedName~ExternalDependencyContractTests|FullyQualifiedName~ArchitectureAnalysisSnapshotTests|FullyQualifiedName~SourceSetExpansionTests
TEST_CORE_UNIT_SHARD_1_FILTER := $(TEST_CORE_UNIT_SHARD_1_FIXTURES)
TEST_CORE_UNIT_SHARD_2_FILTER := $(TEST_UNIT_FILTER)&FullyQualifiedName!~PerTestDurationGuardAttributeTests&FullyQualifiedName!~EnsureBuiltNonDestructiveIntegrationTests&FullyQualifiedName!~FrameworkReferenceContractTests&FullyQualifiedName!~FrameworkReferenceConfigurationTests&FullyQualifiedName!~FrameworkReferenceBaselineIdentityTests&FullyQualifiedName!~ArchitectureAnalysisSessionMethodBodyProjectAwareTests&FullyQualifiedName!~FrameworkReferenceAllowOnlyContractTests&FullyQualifiedName!~ArchitectureProjectRoslynContextResolverTests&FullyQualifiedName!~AspNetSharedFrameworkAcceptanceTests&FullyQualifiedName!~BoundedParallelPartitionRunnerTests&FullyQualifiedName!~CompositionContractTests&FullyQualifiedName!~CelBoundaryArchitectureTests&FullyQualifiedName!~PublicApiSurfaceCheckerTests&FullyQualifiedName!~InheritanceCheckerTests&FullyQualifiedName!~AssemblyIndependenceCheckerTests&FullyQualifiedName!~AcyclicSiblingContractTests&FullyQualifiedName!~ArchitectureContractSchemaInstanceValidationTests&FullyQualifiedName!~ArchitecturePolicyImportTests&FullyQualifiedName!~ArchitectureDiagnosticFormatterTests&FullyQualifiedName!~ExpressionCompilationValidatorTests&FullyQualifiedName!~LayerResolverGlobTests&FullyQualifiedName!~ArchitectureSourceFileFactIndexTests&FullyQualifiedName!~ArchitectureSarifFormatterTests&FullyQualifiedName!~AnalysisCacheStoreTests&FullyQualifiedName!~ArchitectureRoleIndexTests&FullyQualifiedName!~LayerResolverTests&FullyQualifiedName!~ArchitectureAttributeRoleExtractorTests&FullyQualifiedName!~LayoutConventionContractTests&FullyQualifiedName!~AttributeUsageContractTests&FullyQualifiedName!~ArchitecturePublicApiSignatureDetailsCoverageTests&FullyQualifiedName!~TypePlacementContractTests&FullyQualifiedName!~ContextualContractSchemaTests&FullyQualifiedName!~ArchitectureContractSchemaTests&FullyQualifiedName!~ArchitectureBaselineApplicationServiceFakeCompositionTests&FullyQualifiedName!~ArchitectureCoverageSummaryTests&FullyQualifiedName!~ArchitecturePublicApiApplicationServiceTests&FullyQualifiedName!~BaselineSafeAuthoringTests&FullyQualifiedName!~ArchitecturePolicyProvenanceTests&FullyQualifiedName!~PolicyConsistencyCheckTests&FullyQualifiedName!~CelSelectorContextualIntegrationTests&FullyQualifiedName!~InterfaceImplementationContractTests&FullyQualifiedName!~RuleInputCoverageValidationTests&FullyQualifiedName!~ContextualContractValidationTests&FullyQualifiedName!~ArchitecturePolicyEffectiveSchemaValidatorComposedTests&FullyQualifiedName!~EvaluatedBuildInputManifestTests&FullyQualifiedName!~PublicApiSurfaceContractTests&FullyQualifiedName!~AnalysisCacheDiagnosticPayloadConverterTests&FullyQualifiedName!~ArchitectureContractHandlerRegistryTests&FullyQualifiedName!~ArchitectureDeclaredTypeParserTests&FullyQualifiedName!~ArchitectureValidatorTests&FullyQualifiedName!~TestingAdapterTests&FullyQualifiedName!~ExternalDependencyContractTests&FullyQualifiedName!~ArchitectureAnalysisSnapshotTests&FullyQualifiedName!~SourceSetExpansionTests

CORE_TESTS_CSPROJ := $(TESTS_DIR)/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj
CEL_TESTS_CSPROJ := $(TESTS_DIR)/ArchLinterNet.CEL.Tests/ArchLinterNet.CEL.Tests.csproj
CLI_TESTS_CSPROJ := $(TESTS_DIR)/ArchLinterNet.Cli.Tests/ArchLinterNet.Cli.Tests.csproj

test-unit-core-1:  ## Run only Core unit shard 1 (heaviest fixture classes — see docs/internal/core-unit-shard-inventory.md)
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(CORE_TESTS_CSPROJ)" --no-restore --no-build --filter "$(TEST_CORE_UNIT_SHARD_1_FILTER)"

test-unit-core-2:  ## Run only Core unit shard 2 (remainder of the unit bucket, excluding shard 1's fixtures)
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(CORE_TESTS_CSPROJ)" --no-restore --no-build --filter "$(TEST_CORE_UNIT_SHARD_2_FILTER)"

test-unit-other:  ## Run the unit bucket's non-Core assemblies (ArchLinterNet.CEL.Tests, ArchLinterNet.Cli.Tests)
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(CEL_TESTS_CSPROJ)" --no-restore --no-build
	@dotnet test "$(CLI_TESTS_CSPROJ)" --no-restore --no-build

# The aggregate unit command: single build, then the same three invocations test-unit-core-1/
# test-unit-core-2/test-unit-other run underneath as parallel processes off that one build (not
# `$(MAKE) test-unit-core-1` etc., which would each trigger their own redundant/racing build step).
# Mirrors the wait-all-then-check-exit-codes pattern `test` (below) already uses.
test-unit:  ## Run the complete coverage-eligible unit bucket (both Core shards plus CEL/Cli)
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(CORE_TESTS_CSPROJ)" --no-restore --no-build --filter "$(TEST_CORE_UNIT_SHARD_1_FILTER)" & \
	p1=$$!; \
	dotnet test "$(CORE_TESTS_CSPROJ)" --no-restore --no-build --filter "$(TEST_CORE_UNIT_SHARD_2_FILTER)" & \
	p2=$$!; \
	dotnet test "$(CEL_TESTS_CSPROJ)" --no-restore --no-build & \
	p3=$$!; \
	dotnet test "$(CLI_TESTS_CSPROJ)" --no-restore --no-build & \
	p4=$$!; \
	wait $$p1; s1=$$?; \
	wait $$p2; s2=$$?; \
	wait $$p3; s3=$$?; \
	wait $$p4; s4=$$?; \
	if [ $$s1 -ne 0 ] || [ $$s2 -ne 0 ] || [ $$s3 -ne 0 ] || [ $$s4 -ne 0 ]; then exit 1; fi

# Discovers every ArchLinterNet.Core.Tests test via `dotnet vstest --ListFullyQualifiedTests`
# (NOT `dotnet test --list-tests`, which silently ignores --filter and cannot validate anything —
# see docs/internal/core-unit-shard-inventory.md) and checks the shard-1 tokens above against it:
# fails on a dead token (matches nothing — a rename/removal silently shrank shard 1) or a leak
# (a shard-1 token also matches an E2E/packed-artifact fixture). Parses the token lists straight
# out of this file, so there is exactly one authored list, not two kept in sync by hand.
lint-test-shard-membership:  ## Verify Core unit shard tokens are live and don't leak into E2E/packed-artifact
	@dotnet build "$(CORE_TESTS_CSPROJ)" --no-restore --nologo
	@cd "$(PROJECT_ROOT)" && UV_PROJECT_ENVIRONMENT="$(PROJECT_ROOT)/.venv" "$(UV)" run --project tools/pyproject.toml \
		python tools/scripts/verify_core_unit_shards.py --test-mk make/test.mk

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
