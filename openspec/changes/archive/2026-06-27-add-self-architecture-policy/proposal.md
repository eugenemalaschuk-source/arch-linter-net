## Why

After the validation pipeline split (shared application service, runtime contract catalog, handler registry, centralized ignore/baseline context, analysis session indexes, split diagnostics model), the repository's own architecture contract (`architecture/dependencies.arch.yml`) only governed dependency direction between the four packages (`ArchLinterNet.Core`, `Cli`, `Testing`, `Unity`) plus the existing `Core.Scanning` protection. It did not govern the internal boundaries inside `ArchLinterNet.Core` that the split was supposed to establish — nothing stopped the CLI from depending directly on `Core.Execution`/`Core.Contracts` instead of going through the `Core.Validation` application seam, and nothing was actually run in CI to validate the real policy file against the real built assemblies. (GitHub issue #77, parent story #69.)

## What Changes

- Add internal namespace layers to `architecture/dependencies.arch.yml` for `Core.Model`, `Core.Reporting`, `Core.Resolution`, `Core.Contracts`, `Core.Execution`, `Core.Validation` (alongside the existing `core` and `core_scanning` layers).
- Add strict contracts governing those layers: CLI must use the `Core.Validation` application seam instead of `Core.Execution`/`Core.Contracts`/`Core.Resolution`/`Core.Scanning` directly; `Core.Execution` (contract handlers included) must not depend on `Cli`/`Testing`/`Unity`; `Core.Reporting` and `Core.Model` stay dependency-free leaves; `Core.Resolution` and `Core.Scanning` must not depend upward on `Core.Execution`/`Core.Validation`.
- Add a `strict_layers` chain (`cli` → `core_validation` → `core_execution` → `core_model`) expressing the intended seam direction.
- Fix the one real violation this surfaced: the CLI's `baseline generate` command built contract runners and executed contracts directly. Extract `ArchitectureBaselineService` (mirroring the existing `ArchitectureValidationService` seam) into `Core.Validation`, and have the CLI call it instead.
- Break a pre-existing `Core.Validation` ↔ `Core.Execution` namespace cycle (caused by `ValidationTiming` living in `Core.Validation` but being used by `Core.Execution`) by moving `ValidationTiming` to `Core.Reporting`, where it fits alongside other diagnostics-reporting types and has no behavioral effect.
- Add a self-validation test (`SelfArchitecturePolicyTests`) that loads the real `architecture/dependencies.arch.yml` against the real built repository assemblies and asserts it passes in strict mode, and wire `make lint-architecture` to build `Cli` and `Unity` first so all target assemblies are resolvable when the test runs.
- The pre-existing `Core.Contracts` ↔ `Core.Resolution` cycle (contract loading needs resolution helpers; resolution needs contract model types) is left ungoverned — it predates this story, is orthogonal to the validation-pipeline split this story is about, and fixing it would be a separate, larger refactor (avoiding overfitting to a transient internal shape).

## Capabilities

### New Capabilities
- `self-architecture-policy`: defines that the repository's own architecture contract governs its internal validation-pipeline boundaries (application seam, execution/handlers, diagnostics, resolution, scanning) in addition to package-level direction, and that this is actually run against the real repository in the lint gate.

## Impact

- `architecture/dependencies.arch.yml`: new internal layers and contracts.
- `src/ArchLinterNet.Cli/Program.cs`: baseline command now calls `ArchitectureBaselineService` instead of `Core.Execution`/`Core.Contracts` directly; version lookup uses an existing `Core.Validation` type instead of `Core.Contracts`.
- `src/ArchLinterNet.Core/Validation/`: new `ArchitectureBaselineService`, `BaselineGenerationRequest`, `BaselineGenerationOutcome`.
- `src/ArchLinterNet.Core/Reporting/ValidationTiming.cs`: moved from `Core.Validation` (namespace updated, behavior unchanged).
- `tests/ArchLinterNet.Core.Tests/SelfArchitecturePolicyTests.cs`: new self-validation test.
- `make/lint.mk`: `lint-architecture` now builds `Cli` and `Unity` before running the self-validation test.
