## Why

Adopters can currently point ArchLinterNet at stale, mismatched, or missing `bin`/`obj` output with no warning: the tool silently analyzes whatever assembly it happens to find, so a clean checkout, a wrong configuration/TFM, or a code change since the last build produces misleading results instead of a clear failure. `openspec/specs/analysis-build-state-fingerprints/spec.md` (landed in #388) already defines the fingerprint/receipt/preflight-state contract this must implement; #362 is the first consumer that turns that contract into real CLI/Testing behavior.

## What Changes

- Add build-input and analysis-input fingerprint computation (`analysis-build-state/v1` canonical envelope: SHA-256, canonical JSON, repo-relative paths) for the selected project graph, reusing the existing `ArchitectureDiscoveredProject`/`ProjectDiscoveryResult` model from `Core/Discovery`.
- Add a preflight state machine that evaluates the graph before any contract executes and emits exactly one typed diagnostic per project, in this precedence order: `cancelled`, `restore-required`, `missing-artifact`, `wrong-configuration`, `wrong-target-framework`, `wrong-project-output`, `inconsistent-dependency-artifact`, `stale-artifact`, `unverifiable-artifact`, `current`.
- Ordinary validation never restores or builds implicitly: a blocking preflight state stops before contract execution with a diagnostic naming the affected project/assembly, requested configuration/TFM, observed state, and the exact build command where deterministically known.
- Add an explicit opt-in `--ensure-built` CLI flag (and `ArchitectureValidationBuilder.WithEnsureBuilt()` on the Testing API) that evaluates the graph, invokes `dotnet build` once via structured executable+argv (never a shell string, never policy-controlled), stops distinctly on restore/build/cancellation failure, emits an ArchLinterNet build receipt (v1) binding the build-input fingerprint, expected output identities, and PE/PDB/dependency digests, then re-verifies artifacts from that receipt before validation proceeds.
- Add an explicit `--no-restore` CLI flag (and `WithNoRestore()` Testing API method) that preserves `--no-restore` through `--ensure-built` and, without it, fails closed offline with an actionable prerequisite diagnostic instead of attempting network access.
- Render preflight diagnostics through the existing `ArchitectureDiagnostic`/`ArchitectureDiagnosticFormatter`/`ArchitectureSarifFormatter` pipeline so human, JSON, and SARIF output stay normalized projections of one typed finding, with no ANSI color or TTY assumptions.
- Extend the Testing API (`ArchitectureValidationResult`) with a typed preflight result collection alongside existing violation/cycle/coverage collections.

**Non-goals for this change** (deferred, called out explicitly so they are not silently dropped):
- Equivalent-compiler-evidence verification (accepting an existing build without an ArchLinterNet receipt, by inspecting portable-PDB/compiler records directly) — v1 ships receipt-based verification only; a build produced without `--ensure-built` is `unverifiable-artifact` and fails closed. Tracked as a follow-up.
- The immutable completed-session snapshot object and its cross-assertion reuse/disposal lifecycle (#363).
- Verified artifact cache schema and trust-domain controls (#365).
- The full cross-platform/adversarial fixture corpus (#366) — this change adds representative unit/integration coverage for each preflight state, not the exhaustive corpus.
- Timing/counter instrumentation for preflight phases (#374) and phase-level cooperative cancellation beyond a single cancellation check per graph evaluation (#375).
- Replacing MSBuild, general non-.NET build orchestration, changed-file-only validation, executing application startup/runtime containers.

## Capabilities

### New Capabilities
- `analysis-build-state-preflight`: build-input/analysis-input fingerprinting, the preflight state machine and its typed diagnostics, ordinary/no-restore/ensure-built preparation semantics, and the ArchLinterNet build receipt format, exposed through CLI flags and the Testing API.

### Modified Capabilities
- (none — `cli-validation` and `test-adapter` gain new optional flags/methods but their existing requirements are unchanged; no existing spec requirement is altered by this change.)

## Impact

- `src/ArchLinterNet.Core`: new `BuildState/` directory (fingerprint computation, preflight state machine, receipt read/write), new `Model/BuildStatePreflightDiagnostic.cs` (+ evidence/payload types), new `Reporting/ArchitectureDiagnosticFormatter.BuildStatePreflight.cs` and SARIF rule mapping, extended `ValidationOutcome`.
- `src/ArchLinterNet.Cli`: `Commands/Validate/*` gain `--ensure-built` and `--no-restore` options threaded into the validation request; `ValidateCommandHandler` short-circuits on a blocking preflight state.
- `src/ArchLinterNet.Testing`: `ArchitectureValidationBuilder` gains `WithEnsureBuilt()`/`WithNoRestore()`; `ArchitectureValidationResult` gains a `PreflightDiagnostics` property.
- New dependency: none (uses `System.Security.Cryptography.SHA256` and `System.Diagnostics.Process` from the BCL only).
- Tests: new fixtures under `tests/ArchLinterNet.Core.Tests` and `tests/ArchLinterNet.Cli.Tests` covering each preflight state, `--no-restore` offline behavior, and `--ensure-built` success/failure paths.
