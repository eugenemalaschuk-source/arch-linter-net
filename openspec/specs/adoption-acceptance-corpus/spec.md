# Adoption acceptance corpus Specification

## Purpose

Define the reusable synthetic adopter fixtures, deterministic scenario inventory, executable Checkpoint A evidence, and explicit non-release boundary shared by adoption validation work.

## Requirements
### Requirement: Reusable synthetic adopter fixture inventory
The repository SHALL maintain a deterministic, machine-readable inventory of synthetic adoption scenarios and fixture ownership. The inventory SHALL include a minimal single-project policy, a conventional multi-project solution, a multi-host solution with same-named global or top-level `Program` types, a 0.5.0 migration fixture with imports and a legacy baseline, and a clean-checkout fixture with no pre-existing `bin` or `obj` state. Each scenario SHALL identify its fixture shape, owning implemented slices, expected entrypoints, and any report projections.

#### Scenario: Inventory is read by the Checkpoint A entrypoint
- **WHEN** the Checkpoint A test entrypoint runs
- **THEN** it validates that every required fixture shape and implementation child mapping is present in the deterministic inventory

#### Scenario: Later acceptance work needs a fixture
- **WHEN** profiling, consistency, or final release validation adds a scenario
- **THEN** it extends the existing fixture inventory and corpus rather than introducing an independent acceptance system

### Requirement: Executable Checkpoint A integration evidence
The repository SHALL expose an executable NUnit Checkpoint A entrypoint that exercises the implemented adoption-critical scenarios through the CLI and `ArchLinterNet.Testing` surfaces. It SHALL verify imported-policy provenance, exact baseline identity, implemented selector subtraction, package and framework evidence projections, assembly-aware composition identity, deterministic build-state preflight, snapshot reuse, multi-sink rendering reuse, and complete non-TTY human output where each owning slice is implemented.

#### Scenario: CLI and Testing API agree
- **WHEN** one corpus scenario produces canonical findings through the CLI and `ArchLinterNet.Testing`
- **THEN** their canonical finding identity and load-bearing result behavior agree

#### Scenario: Multiple report projections are requested
- **WHEN** one corpus scenario renders human, JSON, and SARIF output
- **THEN** those projections reuse one analysis result and retain their implemented actionable evidence

### Requirement: Checkpoint A remains non-release evidence
The repository SHALL store observed Checkpoint A platform and scenario evidence with an explicit statement that it is internal implementation evidence only. The evidence and manifest SHALL NOT authorize package publication, a public checkpoint release, or product release approval.

#### Scenario: Checkpoint A succeeds on an observed platform
- **WHEN** all scoped Checkpoint A scenarios pass on a recorded platform
- **THEN** the evidence records the platform and scenarios exercised while stating that final release authorization remains owned by Checkpoint B
