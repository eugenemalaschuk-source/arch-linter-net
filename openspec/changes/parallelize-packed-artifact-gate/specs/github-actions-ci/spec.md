## ADDED Requirements

### Requirement: Packed-artifact PR validation fans out one immutable candidate across isolated scenario shards

Pull-request CI SHALL prepare one ephemeral, non-publishable, manifest-bound Checkpoint B candidate and SHALL execute the required packed-artifact scenario inventory as deterministic scenario shards on isolated Windows and Apple Silicon macOS runners. Shards SHALL consume the same candidate manifest/package digest set for the workflow run and SHALL NOT share mutable NuGet caches, tool-install directories, fixture outputs, or temporary state.

The existing authoritative check contexts `Packed Artifact Test Suite (Windows)` and `Packed Artifact Test Suite (Apple Silicon macOS)` SHALL remain stable fan-in checks. A fan-in check SHALL fail when candidate preparation fails, any producer shard fails, shard evidence is missing, or the shard scenario union cannot be merged into a complete canonical platform record.

#### Scenario: Independent Checkpoint B work runs concurrently

- **WHEN** pull-request packed-artifact validation starts after the immutable candidate is prepared
- **THEN** package/entrypoint, adopter-runtime, three consumer-cleanup, and public-API-selector shards run as independently schedulable jobs per supported PR platform
- **AND** no shard depends on another shard
- **AND** every shard consumes the same candidate manifest digest

#### Scenario: Branch-protection check names remain stable

- **WHEN** the sharded producer jobs finish
- **THEN** CI emits `Packed Artifact Test Suite (Windows)` and `Packed Artifact Test Suite (Apple Silicon macOS)` as the authoritative fan-in contexts
- **AND** those contexts succeed only after complete platform shard evidence is merged and validated

#### Scenario: PR candidate packaging is not release publication

- **WHEN** PR CI prepares the packed-artifact candidate
- **THEN** it uses an ephemeral prerelease version scoped to that workflow run
- **AND** the artifact is used only as test input and is never published, tagged, or treated as an official release candidate

### Requirement: Release workflow separates repository correctness from immutable packed-candidate proof

The release workflow SHALL execute repository lint/unit/ordinary-E2E correctness and strict OpenSpec validation once, bind those passed results to the immutable candidate manifest, and SHALL validate the packed candidate separately through the Checkpoint B platform/shard matrix. Generic repository-acceptance stages SHALL NOT rerun the packed-artifact scenario matrix before or after the authoritative immutable-candidate Checkpoint B execution.

Local `make acceptance` SHALL remain the complete lint + unit + ordinary E2E + packed-artifact convenience gate.

#### Scenario: Release preparation does not run a disposable packed candidate

- **WHEN** `prepare-candidate` validates repository correctness before publication authorization
- **THEN** it runs the repository acceptance surface without packed-artifact proof
- **AND** it later creates one immutable candidate that the Checkpoint B shards consume

#### Scenario: Evidence aggregation does not rerun acceptance

- **WHEN** all canonical platform evidence is ready
- **THEN** the final release-evidence job consumes repository-gate evidence already bound to the candidate manifest
- **AND** it does not invoke `make acceptance` or another command that reruns Checkpoint B
