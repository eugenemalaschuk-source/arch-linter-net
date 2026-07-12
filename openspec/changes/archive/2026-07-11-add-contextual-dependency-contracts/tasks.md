## 1. Contextual selector model and matcher

- [x] 1.1 Add `ArchitectureContextSelector` model (`Role`, `Metadata`) to `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` or a new file, distinct from `ArchitectureLayerSelector`.
- [x] 1.2 Add `ArchitectureContextSelectorMatcher` implementing the four-operator metadata grammar (`in` sequence, `any` = `"*"`, `not-equal-to-source` = `!{source.metadata.<key>}`, `exact` literal fallback), reusing `ArchitectureLayerTypeMatcher`'s string/bool/decimal equality helpers where practical.
- [x] 1.3 Unit tests for each operator: exact match/mismatch, `in` match/mismatch, `any` with present/absent key, `not-equal-to-source` match/mismatch/missing-source-key.

## 2. Contract models

- [x] 2.1 Add `ArchitectureContextDependencyContract` (`name`, `id`, `source`, `forbidden: List<ArchitectureContextSelector>`, `exclude`, `ignored_violations`, `reason`) in `src/ArchLinterNet.Core/Contracts/Families/ContextDependencyContractFamily.cs`, extending `ArchitectureContractGroups` with `StrictContextDependencies`/`AuditContextDependencies`.
- [x] 2.2 Add `ArchitectureContextAllowOnlyContract` (`source`, `allowed`, `exclude`, `ignored_violations`, `reason`) in `src/ArchLinterNet.Core/Contracts/Families/ContextAllowOnlyContractFamily.cs`, extending `ArchitectureContractGroups` with `StrictContextAllowOnly`/`AuditContextAllowOnly`.

## 3. Checker/evaluation logic

- [x] 3.1 Implement `CheckContextDependencyContract` in `ArchitectureAnalysisSession.Checking.cs` (or new partial): resolve source-matching types via `ArchitectureRoleIndex`, scan references via `ArchitectureReferenceScanner`, evaluate `forbidden`/`exclude` selectors per source-type instance for `not-equal-to-source` support.
- [x] 3.2 Implement `CheckContextAllowOnlyContract` mirroring 3.1 with `allowed`/`exclude` semantics.
- [x] 3.3 Apply existing `ArchitectureContractExecutionContext`/`ArchitectureIgnoreMatcher` ignored-violation handling to both new checkers.
- [x] 3.4 Register both families in `ArchitectureContractFamilyRegistry.All` (descriptors, `OwnedContractTypes`, no `ConfigurationContributor` layer-name collection since contextual contracts reference no `layers.<name>`).
- [x] 3.5 Record each contextual contract's selector `(role, metadata key)` references as coverage-participating consumption (session-level registration hook) per the `semantic-classification-model` delta requirement.

## 4. Diagnostics

- [x] 4.1 Add `ArchitectureDiagnosticKind` values for the two new families.
- [x] 4.2 Add `ContextDependencyPayload`/`ContextDependencyDiagnostic` (source role/metadata, target role/metadata, matched selector kind) mirroring `Model/DependencyPayload.cs`/`Model/DependencyDiagnostic.cs`.
- [x] 4.3 Add `ContextAllowOnlyPayload`/`ContextAllowOnlyDiagnostic` mirroring 4.2 for allow-only semantics.
- [x] 4.4 Verify `ArchitectureDiagnosticMapper.FromViolation` dispatches the new payloads without modification; add JSON/human formatting coverage in `ArchitectureDiagnosticFormatter` if new fields need explicit rendering.

## 5. Baseline support

- [x] 5.1 Add `context_dependencies`/`context_allow_only` cases to `ArchitectureBaselineModels.cs`'s `GetGroup`/`SetGroup`/`GroupNames` switch, mirroring the `dependency`/`allow_only` cases.
- [x] 5.2 Baseline generation/comparison tests for both new families.

## 6. Schema

- [x] 6.1 Add `$defs.contextSelector` to `schema/dependencies.arch.schema.json` (role required, metadata values accepting scalar/`"*"`/`not-equal-to-source` pattern/array).
- [x] 6.2 Add `$defs.contextDependencyContract` and `$defs.contextAllowOnlyContract`.
- [x] 6.3 Add `strict_context_dependencies`/`audit_context_dependencies`/`strict_context_allow_only`/`audit_context_allow_only` properties under `$defs.contracts`.
- [x] 6.4 Schema validation tests (valid and invalid documents) alongside existing `ArchitectureContractSchemaTests.cs`/`ArchitectureContractSchemaInstanceValidationTests.cs`.

## 7. Tests

- [x] 7.1 Add Sales/Inventory/SharedKernel-style Roslyn in-memory test fixtures (mirroring `SelectorContractTestFixtures.cs` conventions) covering same-domain (green), cross-domain (violating), `SharedKernel` exclusion cases.
- [x] 7.2 Tests: missing source metadata, missing target metadata, same-domain match (no violation), different-domain violation, exclusions, strict mode (build fails), audit mode (reports only).
- [x] 7.3 Tests: JSON diagnostic evidence fields, human-readable diagnostic distinguishability from namespace/layer dependency violations.
- [x] 7.4 CLI-level tests in `tests/ArchLinterNet.Cli.Tests/` (`CliArchitectureTests.cs`/`CliHandlerCoverageTests.cs`) exercising the new families end-to-end.

## 8. Docs and examples

- [x] 8.1 Add `docs/contracts/context-dependency.md` and `docs/contracts/context-allow-only.md`, update `mkdocs.yml` TOC.
- [x] 8.2 Update `docs/policy-format/semantic-classification.md` with the contextual selector operator vocabulary and a comparison against `layers.<name>.selector`.
- [x] 8.3 Update `docs/policy-format/supported-capabilities.md`, `docs/ai/capabilities.md`, `archlinternet.capabilities.json`.
- [x] 8.4 Add a contextual contract example to `samples/policies/modular-monolith.yml` or a new sample policy.

## 9. Spec synchronization and archive

- [x] 9.1 Run `openspec validate --all` after implementation to confirm delta specs remain consistent.
- [ ] 9.2 Run `openspec archive add-contextual-dependency-contracts` after implementation, tests, and doc updates are complete.
