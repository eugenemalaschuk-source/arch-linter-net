## 1. Core fingerprinting

- [x] 1.1 Add `Core/BuildState/BuildStateCanonicalHasher.cs` — SHA-256 content-digest fingerprint over the project file and relevant compiled/imported source content (v1 coarse-grained implementation of `analysis-build-state/v1`; see spec.md scope note).
- [~] 1.2 ~~Add `Core/BuildState/ProjectBuildManifest.cs` and `ProjectBuildManifestBuilder.cs`~~ — not built as a separate granular manifest type; `BuildStateCanonicalHasher` computes the fingerprint directly. Deferred: granular per-field manifest (SDK identity, per-compiler-option digests, analyzer/generator identities).
- [~] 1.3 ~~Add `Core/BuildState/BuildInputFingerprint.cs` / `AnalysisInputFingerprint.cs`~~ — fingerprints are computed as plain strings by `BuildStateCanonicalHasher`/`BuildReceiptV1` rather than dedicated wrapper types; no behavior gap, just a simpler type shape.
- [~] 1.4 ~~Add `Core/BuildState/ExpectedBuildOutputIdentity.cs`~~ — expected output path is carried directly on `BuildStatePreflightEvidence.ExpectedOutputPath` instead of a separate type.

## 2. Preflight state machine and diagnostics

- [x] 2.1 Add `Core/Model/BuildStatePreflightState.cs` (enum, 10 states, precedence order) and `BuildStatePreflightEvidence.cs`.
- [x] 2.2 Add `Core/Model/BuildStatePreflightDiagnostic.cs : ArchitectureDiagnostic` with `Kind => ArchitectureDiagnosticKind.BuildStatePreflight`.
- [x] 2.3 Extend `ArchitectureDiagnosticKind` enum with `BuildStatePreflight`.
- [x] 2.4 Add `Core/BuildState/BuildStatePreflightEvaluator.cs` — evaluates the graph, emits one diagnostic per project following precedence order.

## 3. Receipt and ensure-built preparation

- [x] 3.1 Add `Core/BuildState/BuildReceiptV1.cs` (DTO) and `BuildReceiptStore.cs` (read/write JSON receipt file alongside build output).
- [x] 3.2 Add `Core/BuildState/BuildPreparationMode.cs` (enum: `Ordinary`, `EnsureBuilt`; `NoRestore` implemented as an independent request flag composable with either mode, not a third enum member — see design.md decision 2 amendment).
- [x] 3.3 Add `Core/BuildState/BuildStatePreparationService.cs` — orchestrates preflight for both modes plus the independent `NoRestore` flag; owns the single `dotnet build` `ProcessStartInfo`/`ArgumentList` invocation for `EnsureBuilt`.
- [x] 3.4 Implement post-build re-verification: after `EnsureBuilt` completes, re-resolve built assemblies and re-run fingerprint/artifact checks (twice, for TOCTOU protection) before returning a non-blocking result.
- [x] 3.5 Implement `NoRestore` offline failure path: detect missing `obj/project.assets.json` and emit `restore-required` without network access.

## 4. ValidationOutcome and formatters

- [x] 4.1 Extend `Core/Validation/ValidationOutcome.cs` with `PreflightDiagnostics` and `PreflightBlocked`.
- [x] 4.2 Add `Core/Reporting/ArchitectureDiagnosticFormatter.BuildStatePreflight.cs` (human + JSON rendering).
- [~] 4.3 ~~Extend `Core/Reporting/ArchitectureSarifFormatter.cs` with a `build-state-preflight` rule mapping~~ — deliberately deferred; preflight diagnostics are JSON/human only in this change, consistent with the existing precedent that coverage/unmatched-ignore/policy-consistency findings are also not part of `--format sarif` (documented in spec.md and the CLI `--format` help text).

## 5. CLI wiring

- [x] 5.1 Add `--ensure-built`, `--no-restore`, `--configuration`, `--framework` options to `Cli/Commands/Validate/ValidateCommandDefinition.cs` and `ValidateCommandOptions.cs`.
- [x] 5.2 Thread the preparation mode into `ValidateCommandHandler.Execute` / `ExecuteValidation`; short-circuit before contract execution when `PreflightBlocked`.
- [~] 5.3 ~~Update `Commands/PolicyDiagnosticOutputWriter.cs`~~ — that writer is specific to `ArchitecturePolicyDiagnostic` (policy load/validation errors); preflight human rendering was instead added directly to `ValidateCommandHandler.WriteHumanOutput` via the new `ICliRuntime.FormatBuildStatePreflightForHumans`, which is the correct existing seam for outcome-shaped sections (mirrors how coverage/classification sections are rendered).

## 6. Testing API wiring

- [x] 6.1 Add `WithEnsureBuilt(configuration?, targetFramework?)` and `WithNoRestore()` to `ArchitectureValidationBuilder.cs`.
- [x] 6.2 Add `PreflightDiagnostics` and `PreflightBlocked` to `ArchitectureValidationResult.cs`, and preflight rendering to `ShouldPass()`'s failure message.

## 7. Tests

- [x] 7.1 `Core.Tests`: fingerprint determinism (same inputs → same digest) and invalidation (source content change → different digest).
- [x] 7.2 `Core.Tests`: one test per preflight state (missing-artifact, unverifiable-artifact, current, stale-artifact ×2, wrong-configuration, wrong-target-framework, wrong-project-output, inconsistent-dependency-artifact, cancelled, restore-required) using fixture project graphs.
- [x] 7.3 `Core.Tests`: `EnsureBuilt` success path (real `dotnet build` + receipt + re-verify) as an integration test; build-failure path is exercised indirectly via `InvokeDotnetBuild`'s non-zero-exit branch (not covered by a dedicated failing-build fixture test — follow-up).
- [x] 7.4 `Core.Tests`: `NoRestore` offline `restore-required` and pass-through-when-restored paths.
- [~] 7.5 `Cli.Tests`: `--ensure-built`/`--no-restore`/`--configuration`/`--framework` flag-to-`ValidationRequest` mapping is covered; a full CLI-process end-to-end JSON/SARIF rendering test for a blocking preflight state was not added (covered indirectly by the Core-level JSON/human formatter tests plus a manual CLI smoke test during validation) — follow-up.

## 8. Validation and documentation

- [x] 8.1 Run `rtk make fmt` and inspect formatting changes.
- [x] 8.2 Run `rtk make acceptance` (lint + full test suite); fixed a self-architecture layer-boundary violation (`BuildState` importing `Execution` — resolved by introducing `BuildStateResolvedAssemblies` instead of reusing `Execution.ResolutionResult`), an exhaustive layer-template test fixture gap, a `code-size` threshold breach (split the `FormatResultForCiArtifacts` interface overload into the `BuildStatePreflight` partial), and two behavioral regressions in `analysis.target_assemblies`-only policies (preflight now explicitly does not apply to that mode — see the new "preflight applies to project-graph-driven resolution" requirement).
- [x] 8.3 CLI `--help` text documents `--ensure-built`/`--no-restore`/`--configuration`/`--framework` (this repository's CLI reference is the in-tool help text; no separate `docs/` reference page exists for other `validate` flags either).
- [x] 8.4 Synchronize `openspec/specs/analysis-build-state-preflight/spec.md` with actual implemented behavior; run `openspec validate --all`; run `openspec archive analysis-build-state-preflight`.
