.PHONY: test-repository _repository-acceptance-test prepare-packed-artifact-candidate \
	test-packed-artifact-package-and-entrypoints test-packed-artifact-adopter-runtime-core \
	test-packed-artifact-adopter-runtime-extended \
	test-packed-artifact-consumer-cleanup-policy-foundation \
	test-packed-artifact-consumer-cleanup-configuration-and-identity \
	test-packed-artifact-consumer-cleanup-source-set-authoring \
	test-packed-artifact-public-api-surface-selector-snapshot-and-role \
	test-packed-artifact-public-api-surface-selector-lifecycle

# Checkpoint B CI/release sharding. The ordinary `test-packed-artifact` target remains the complete
# local gate and discovers all CheckpointBReleaseGateTests methods in one NUnit process. These
# method-specific targets exist so isolated CI workspaces can spend runner-minutes to reduce the
# packed-artifact wall-clock while preserving the exact scenario union.
TEST_PACKED_ARTIFACT_PACKAGE_AND_ENTRYPOINTS_FILTER := FullyQualifiedName~CheckpointBReleaseGateTests.PackedCandidate_PackageAndEntrypoints
TEST_PACKED_ARTIFACT_ADOPTER_RUNTIME_CORE_FILTER := FullyQualifiedName~CheckpointBReleaseGateTests.PackedCandidate_AdopterRuntimeCore
TEST_PACKED_ARTIFACT_ADOPTER_RUNTIME_EXTENDED_FILTER := FullyQualifiedName~CheckpointBReleaseGateTests.PackedCandidate_AdopterRuntimeExtended
TEST_PACKED_ARTIFACT_CONSUMER_CLEANUP_POLICY_FOUNDATION_FILTER := FullyQualifiedName~CheckpointBReleaseGateTests.PackedCandidate_ConsumerCleanupPolicyFoundation
TEST_PACKED_ARTIFACT_CONSUMER_CLEANUP_CONFIGURATION_AND_IDENTITY_FILTER := FullyQualifiedName~CheckpointBReleaseGateTests.PackedCandidate_ConsumerCleanupConfigurationAndIdentity
TEST_PACKED_ARTIFACT_CONSUMER_CLEANUP_SOURCE_SET_AUTHORING_FILTER := FullyQualifiedName~CheckpointBReleaseGateTests.PackedCandidate_ConsumerCleanupSourceSetAuthoring
TEST_PACKED_ARTIFACT_PUBLIC_API_SURFACE_SELECTOR_SNAPSHOT_AND_ROLE_FILTER := FullyQualifiedName~CheckpointBReleaseGateTests.PackedCandidate_PublicApiSurfaceSelectorSnapshotAndRole
TEST_PACKED_ARTIFACT_PUBLIC_API_SURFACE_SELECTOR_LIFECYCLE_FILTER := FullyQualifiedName~CheckpointBReleaseGateTests.PackedCandidate_PublicApiSurfaceSelectorLifecycle

CHECKPOINT_B_CANDIDATE_VERSION ?= 0.6.1
CHECKPOINT_B_CANDIDATE_DIR ?= $(PROJECT_ROOT)/artifacts/checkpoint-b-candidate
CHECKPOINT_B_CANDIDATE_MANIFEST ?= $(CHECKPOINT_B_CANDIDATE_DIR)/package-manifest.json

# Pull-request CI prepares one immutable candidate and distributes it to every OS/scenario shard.
# Release CI has its own version-calculation/packing stage and supplies the same environment contract.
prepare-packed-artifact-candidate:  ## CI: pack one manifest-bound Checkpoint B candidate for isolated shards
	@test -n "$(ARCH_LINTER_SOURCE_SHA)" || (echo "ARCH_LINTER_SOURCE_SHA is required" >&2; exit 2)
	@rm -rf "$(CHECKPOINT_B_CANDIDATE_DIR)"
	@mkdir -p "$(CHECKPOINT_B_CANDIDATE_DIR)"
	@dotnet pack "$(SLNX)" --configuration Release --output "$(CHECKPOINT_B_CANDIDATE_DIR)" \
		--no-restore -p:Version="$(CHECKPOINT_B_CANDIDATE_VERSION)" \
		-p:PackageVersion="$(CHECKPOINT_B_CANDIDATE_VERSION)" --nologo
	@python3 "$(PROJECT_ROOT)/tools/release/package_manifest.py" create \
		--packages-dir "$(CHECKPOINT_B_CANDIDATE_DIR)" \
		--version "$(CHECKPOINT_B_CANDIDATE_VERSION)" \
		--source-commit "$(ARCH_LINTER_SOURCE_SHA)" \
		--output "$(CHECKPOINT_B_CANDIDATE_MANIFEST)"

# Shards deliberately build only the Core test project. Product packages come from the supplied
# immutable feed; building the whole solution again in every shard would only recreate source-tree
# binaries that are not the release evidence under test.
test-packed-artifact-package-and-entrypoints:  ## Run Checkpoint B package/entrypoint shard
	@dotnet build "$(CORE_TESTS_CSPROJ)" --no-restore --nologo
	@dotnet test "$(CORE_TESTS_CSPROJ)" --no-restore --no-build --filter "$(TEST_PACKED_ARTIFACT_PACKAGE_AND_ENTRYPOINTS_FILTER)"

test-packed-artifact-adopter-runtime-core:  ## Run Checkpoint B core adopter/runtime shard
	@dotnet build "$(CORE_TESTS_CSPROJ)" --no-restore --nologo
	@dotnet test "$(CORE_TESTS_CSPROJ)" --no-restore --no-build --filter "$(TEST_PACKED_ARTIFACT_ADOPTER_RUNTIME_CORE_FILTER)"

test-packed-artifact-adopter-runtime-extended:  ## Run Checkpoint B extended adopter/runtime shard
	@dotnet build "$(CORE_TESTS_CSPROJ)" --no-restore --nologo
	@dotnet test "$(CORE_TESTS_CSPROJ)" --no-restore --no-build --filter "$(TEST_PACKED_ARTIFACT_ADOPTER_RUNTIME_EXTENDED_FILTER)"

test-packed-artifact-consumer-cleanup-policy-foundation:  ## Run Checkpoint B consumer-cleanup policy shard
	@dotnet build "$(CORE_TESTS_CSPROJ)" --no-restore --nologo
	@dotnet test "$(CORE_TESTS_CSPROJ)" --no-restore --no-build --filter "$(TEST_PACKED_ARTIFACT_CONSUMER_CLEANUP_POLICY_FOUNDATION_FILTER)"

test-packed-artifact-consumer-cleanup-configuration-and-identity:  ## Run Checkpoint B consumer-cleanup configuration shard
	@dotnet build "$(CORE_TESTS_CSPROJ)" --no-restore --nologo
	@dotnet test "$(CORE_TESTS_CSPROJ)" --no-restore --no-build --filter "$(TEST_PACKED_ARTIFACT_CONSUMER_CLEANUP_CONFIGURATION_AND_IDENTITY_FILTER)"

test-packed-artifact-consumer-cleanup-source-set-authoring:  ## Run Checkpoint B consumer-cleanup source-set shard
	@dotnet build "$(CORE_TESTS_CSPROJ)" --no-restore --nologo
	@dotnet test "$(CORE_TESTS_CSPROJ)" --no-restore --no-build --filter "$(TEST_PACKED_ARTIFACT_CONSUMER_CLEANUP_SOURCE_SET_AUTHORING_FILTER)"

test-packed-artifact-public-api-surface-selector-snapshot-and-role:  ## Run Checkpoint B public-API selector snapshot/role shard
	@dotnet build "$(CORE_TESTS_CSPROJ)" --no-restore --nologo
	@dotnet test "$(CORE_TESTS_CSPROJ)" --no-restore --no-build --filter "$(TEST_PACKED_ARTIFACT_PUBLIC_API_SURFACE_SELECTOR_SNAPSHOT_AND_ROLE_FILTER)"

test-packed-artifact-public-api-surface-selector-lifecycle:  ## Run Checkpoint B public-API selector lifecycle shard
	@dotnet build "$(CORE_TESTS_CSPROJ)" --no-restore --nologo
	@dotnet test "$(CORE_TESTS_CSPROJ)" --no-restore --no-build --filter "$(TEST_PACKED_ARTIFACT_PUBLIC_API_SURFACE_SELECTOR_LIFECYCLE_FILTER)"

# Repository correctness without the packed candidate gate. Release workflows use this once to
# prove source-tree lint/unit/E2E correctness, then prove the immutable packed candidate separately.
# Local `make acceptance` remains the complete lint + unit + E2E + packed-artifact gate.
test-repository:  ## Run unit + ordinary E2E correctness without the packed-artifact release gate
	@dotnet build "$(SLNX)" --no-restore --nologo
	@dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_UNIT_FILTER)" & \
	p1=$$!; \
	dotnet test "$(SLNX)" --no-restore --no-build --filter "$(TEST_E2E_FILTER)" & \
	p2=$$!; \
	wait $$p1; s1=$$?; \
	wait $$p2; s2=$$?; \
	if [ $$s1 -ne 0 ] || [ $$s2 -ne 0 ]; then exit 1; fi

_repository-acceptance-test: | _lint-dotnet
	@$(MAKE) test-repository
