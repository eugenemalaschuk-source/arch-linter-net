## 1. Diagnostic model

- [x] 1.1 Add `ArchitectureDiagnosticKind.PolicyConsistency` to the kind enum.
- [x] 1.2 Add `PolicyConsistencyDiagnostic` record (CheckKind, Reason, ConflictingContractIds, ConflictingContractNames, Layers, RepresentativeType) under `src/ArchLinterNet.Core/Model/`.
- [x] 1.3 Extend `ArchitectureDiagnosticFormatter` to render `PolicyConsistencyDiagnostic` in human-readable and CI JSON output.

## 2. Configuration schema

- [x] 2.1 Add `analysis.policy_consistency: error|warn|off` to `ArchitectureAnalysisConfiguration`, defaulting to `error`.
- [x] 2.2 Validate the value in `ArchitectureValidationService`, throwing `InvalidOperationException` for anything other than `error`/`warn`/`off`.
- [x] 2.3 Update the JSON schema under `schema/` for the new `analysis.policy_consistency` field.

## 3. Policy consistency checker

- [x] 3.1 Add `ArchitectureContractRunner.CheckPolicyConsistency` operating on the expanded `ArchitectureContractGroups` (post `LayerTemplateExpander.Expand()`).
- [x] 3.2 Implement duplicate contract ID detection across strict/audit families and expanded template IDs.
- [x] 3.3 Implement allow-only vs forbidden dependency conflict detection (same source/target layer pair).
- [x] 3.4 Implement independence vs explicit allowed/ordered dependency conflict detection.
- [x] 3.5 Implement protected-surface `allowed_importers` vs strict forbidden/protected rule conflict detection.
- [x] 3.6 Implement overlapping layer definition detection (reuse `ArchitectureLayerResolver.MatchNamespace`/type enumeration from the empty-layer check; exclude external-vs-internal overlap).
- [x] 3.7 Implement unreachable-contract detection (structurally impossible source/target layer patterns).
- [x] 3.8 Ensure all findings are emitted in deterministic order (stable sort by contract/layer identifiers, no hash-set/dictionary enumeration leakage).

## 4. Pipeline wiring

- [x] 4.1 Add a `policy_consistency_check` step to `ArchitectureValidationService.Validate`, running after `configuration_check` and before `contract_checks`.
- [x] 4.2 Add `ValidationOutcome.PolicyConsistencyFindings` and fold findings into `Passed` according to resolved `analysis.policy_consistency` severity.
- [x] 4.3 Confirm CLI `validate`, public `ArchitectureValidator`, and the Testing adapter all surface findings without per-caller changes (shared pipeline).

## 5. Tests

- [x] 5.1 Green policy (no contradictions) — zero findings, `Passed` unaffected.
- [x] 5.2 Duplicate contract IDs (explicit and expanded-template) — finding reported, contract names/IDs correct.
- [x] 5.3 Allow-only vs forbidden conflict — finding reported with both contract identifiers and layers.
- [x] 5.4 Independence vs explicit dependency conflict — finding reported.
- [x] 5.5 Protected-importer conflict — finding reported.
- [x] 5.6 Layer overlap (internal/internal flagged; internal/external not flagged) — finding includes layer names and representative type.
- [x] 5.7 Unreachable contract (structurally impossible layer pattern) — finding reported.
- [x] 5.8 Strict/audit interaction — duplicate IDs and conflicts detected across strict and audit families, not just within one.
- [x] 5.9 Severity behavior — `error` fails `Passed`, `warn` reports without failing, `off` suppresses findings and does not affect `Passed`; invalid value throws `InvalidOperationException`.
- [x] 5.10 Determinism — repeated validation of the same policy produces identical ordered findings.
- [x] 5.11 CLI/public-API/Testing-adapter integration tests confirming findings surface through all three entry points.

## 6. Docs and spec sync

- [x] 6.1 Update `docs/architecture/` (or relevant docs site pages) describing the policy-consistency pass and `analysis.policy_consistency` setting.
- [x] 6.2 Update sample policies under `samples/` if any now trigger findings, or add a dedicated sample demonstrating the setting.
- [x] 6.3 Run `openspec validate --all` after archiving to confirm specs remain consistent.
