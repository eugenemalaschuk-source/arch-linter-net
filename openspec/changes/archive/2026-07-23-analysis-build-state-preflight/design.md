## Context

`openspec/specs/analysis-build-state-fingerprints/spec.md` and `docs/internal/analysis-build-state-blueprint.md` already define the normative fingerprint/receipt/preflight contract. This design turns that contract into concrete types and call sites in the existing pipeline: `Core/Discovery` (project graph), `Core/Execution/ArchitectureAssemblyResolutionService` (assembly resolution), `Core/Validation/ValidationOutcome` (aggregated result), `Core/Reporting` (human/JSON/SARIF formatters), `Cli/Commands/Validate` (CLI entrypoint), and `ArchLinterNet.Testing/ArchitectureValidationBuilder` (Testing API).

## Goals / Non-Goals

**Goals:**
- Compute build-input and analysis-input fingerprints for the selected project graph per the `analysis-build-state/v1` envelope.
- Run one preflight state machine pass over the graph before any contract executes; stop closed on a blocking state.
- Support ordinary (no build/restore), `--no-restore`, and `--ensure-built` preparation modes with distinct, deterministic failure diagnostics.
- Emit and verify an ArchLinterNet build receipt (v1) as the authoritative artifact-freshness proof.
- Render preflight diagnostics as one more typed `ArchitectureDiagnostic` family across human/JSON/SARIF and the Testing API.

**Non-Goals** (see proposal.md for the full list): equivalent-compiler-evidence verification without a receipt; the immutable session snapshot object (#363); verified artifact cache (#365); the full adversarial fixture corpus (#366); timing instrumentation (#374); multi-phase cooperative cancellation (#375).

## Decisions

### 1. Where fingerprinting/preflight lives: new `Core/BuildState/` directory

Preflight is not a contract family (it doesn't check architecture rules) and it runs *before* contract execution, so it does not belong under `Core/Contracts/Families`. It is closest in spirit to `Core/Discovery` and `Core/Execution`. A dedicated `Core/BuildState/` directory keeps fingerprinting, the state machine, and receipt I/O together and mirrors the existing directory-per-concern layout (`Discovery/`, `Resolution/`, `Execution/`, `Reporting/`).

Key types:
- `BuildStateCanonicalHasher` — canonical UTF-8 JSON serialization (ordinal-sorted object keys, declared sort key for set-like arrays) + SHA-256 digest, per the envelope rules in the fingerprint spec.
- `ProjectBuildManifest` / `ProjectBuildManifestBuilder` — builds the v1 project/build manifest (project key, graph edges, configuration/platform/TFM/RID, project+import content digests, SDK/global properties, compile items, analyzer/generator identities, compiler options, reference identities) from an `ArchitectureDiscoveredProject` plus filesystem reads.
- `EffectiveBuildInputFingerprint` / `EffectiveAnalysisInputFingerprint` — computed digests over the manifest set (+ policy provenance for the analysis fingerprint).
- `ExpectedBuildOutputIdentity` — one per selected project/configuration/TFM/platform/RID, derived from `ArchitectureDiscoveredProject` (assembly name, output kind, expected output role/path, whether PDB/deps/runtimeconfig are expected).
- `BuildReceiptV1` — the on-disk receipt DTO (JSON) written by `--ensure-built`, containing the build-input fingerprint, expected output identities, and PE/PDB/reference/dependency SHA-256 digests.
- `BuildStatePreflightEvaluator` — the state machine: for each expected output identity, checks (in precedence order) cancellation, restore-required, artifact presence, configuration/TFM/output-kind match, dependency-artifact consistency, staleness (receipt build-input fingerprint vs. freshly computed one), and receipt verifiability; emits exactly one `BuildStatePreflightDiagnostic` per project.
- `BuildStatePreparationService` — orchestrates the three modes (`Ordinary`, `NoRestore`, `EnsureBuilt`); only this service is allowed to construct a `dotnet build` invocation.

### 2. Preparation modes as an explicit enum, not boolean flags threaded ad hoc

`BuildPreparationMode { Ordinary, NoRestore, EnsureBuilt }` is passed once into `BuildStatePreparationService.Prepare(...)`, which returns a `BuildStatePreflightResult { IReadOnlyList<BuildStatePreflightDiagnostic> Diagnostics, bool HasBlockingState }`. CLI and Testing API both funnel into this one method — avoids duplicating state-machine logic between the two entrypoints (rejected alternative: separate CLI-only preflight logic with Testing API reimplementing a subset).

### 3. `dotnet build` invocation: `System.Diagnostics.ProcessStartInfo` with `ArgumentList`, never `Arguments` string

Per the blueprint's security/trust boundary ("structured argv prevents shell injection"), `EnsureBuilt` mode always sets `UseShellExecute = false` and populates `ArgumentList` (`["build", projectOrSolutionPath, "-c", configuration, "--nologo"]`, plus `"--no-restore"` when requested). No part of the argv is ever sourced from policy YAML, baseline, receipt, or cache content — only from the CLI option / Testing API method the caller invoked directly. The graph is built once (`dotnet build` on the discovered solution/project set), not per-project, per the blueprint's "build the graph once rather than per contract" rule.

### 4. Receipt-based verification only for v1 (rejected: equivalent-compiler-evidence path)

The blueprint allows accepting an ordinary (non-ArchLinterNet) build via portable-PDB/compiler-record inspection (document checksums, compilation options, metadata-reference identities, PE/PDB association). That requires a portable-PDB parser and compiler-manifest reconstruction that is a substantial standalone effort and is not required by #362's acceptance criteria (which describe "product-owned build mode" and clean-checkout/`--ensure-built` behavior, not build-tool-agnostic verification). v1 ships the receipt path only: any artifact without a valid, matching ArchLinterNet receipt is `unverifiable-artifact` and fails closed, satisfying "fail closed when assemblies are ... stale" without silently downgrading to a weaker guarantee. This is called out as an explicit non-goal in proposal.md.

### 5. Diagnostic family follows the existing `ArchitectureDiagnostic` pattern

`BuildStatePreflightDiagnostic(ContractName, ContractId) : ArchitectureDiagnostic` with `Kind => ArchitectureDiagnosticKind.BuildStatePreflight`, plus a `BuildStatePreflightState` enum (the ten states) and a `BuildStatePreflightEvidence` record (project key, expected vs. observed configuration/TFM/output, searched paths, build command). `ContractName`/`ContractId` are synthesized as a stable `"build-state-preflight"` / project key pair so the diagnostic fits the existing `ArchitectureViolation`-adjacent rendering pipeline without requiring a real policy contract to exist. Rendered via a new `ArchitectureDiagnosticFormatter.BuildStatePreflight.cs` partial (human + JSON) and a new SARIF rule id (`archlinternet/build-state-preflight/<state>`) in `ArchitectureSarifFormatter`.

### 6. `ValidationOutcome` gains `PreflightDiagnostics` and a `Blocked` flag

`ValidationOutcome` already aggregates `Violations`/`Cycles`/`CoverageFindings`. Adding `IReadOnlyList<BuildStatePreflightDiagnostic> PreflightDiagnostics` and `bool PreflightBlocked` lets `ValidateCommandHandler` and `ArchitectureValidationBuilder` short-circuit identically: when `PreflightBlocked` is true, no contract executes and only preflight diagnostics are reported.

## Risks / Trade-offs

- [Risk] Full v1 project/build manifest (analyzer/generator identities, all compiler options) is large surface to get exactly right → Mitigation: implement the fields that drive real preflight states for this change (project/source/import content digests, configuration/TFM/platform/RID, expected output identity, reference identities); leave clearly-marked extension points (documented, not silently omitted) for remaining manifest fields the fingerprint spec lists, consistent with "no gold-plating" — those fields are display/evidence-only for #362's acceptance scenarios.
- [Risk] `dotnet build` invocation behavior differs slightly across the Windows/Linux/macOS `dotnet` CLI → Mitigation: `ArgumentList`-based invocation avoids shell-quoting differences entirely; process exit code is the only cross-platform signal consulted.
- [Risk] Skipping equivalent-compiler-evidence verification means a user who built manually (without `--ensure-built`) always sees `unverifiable-artifact` → Mitigation: diagnostic message explicitly names `--ensure-built` as the exact remediation command; documented as a known v1 limitation, not silently degraded.
- [Trade-off] Preflight synthesizes a `ContractName`/`ContractId` rather than requiring a real policy contract, so it participates in the existing rendering pipeline without a schema change to contract definitions; revisit if a future change wants preflight severity to be policy-configurable (out of scope here — the issue requires it to fail closed unconditionally).

## Migration Plan

No migration: this is new, additive, off-by-default behavior (ordinary validation gains fail-closed checks it didn't have before, which is the intended fix, not a breaking behavior change for callers who already had current artifacts). `--ensure-built`/`--no-restore` are new opt-in flags. No existing CLI output schema field is removed or renamed.

## Open Questions

None blocking implementation; equivalent-compiler-evidence verification is deferred to a follow-up issue rather than an open question for this change.
