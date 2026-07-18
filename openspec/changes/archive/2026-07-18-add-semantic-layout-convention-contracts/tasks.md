## 1. cel-policy-model spec location extension

- [x] 1.1 Extend `ArchitecturePolicyDocumentLoader`'s raw-YAML key validators so the two new `files_matching.when` locations (`strict_layout_conventions[*]`, `audit_layout_conventions[*]`) accept `when`, following the `allowWhen`-parameter, call-site-scoped opt-in pattern from `core-cel-integration` D4 — no other layout convention field accepts `when`.

## 2. Contract model (YAML schema types)

- [x] 2.1 Add `ArchitectureLayoutFileMatcher` (`files_matching`) to `Contracts/Families/` with `folder_segment`, `namespace_segment`, `file_name_suffix`, `file_name_prefix`, `when` (YamlMember) plus `[YamlIgnore] internal CelCompiledPredicate? CompiledWhen`, `WhenLocation`, `WhenContractName` mirroring `ArchitectureContextSelector`.
- [x] 2.2 Add `ArchitectureRequireMatchingInterface` (or equivalent) with optional `name_prefix` (default `"I"`).
- [x] 2.3 Add `ArchitectureLayoutConventionContract : IArchitectureContract` with `name`, `id`, `files_matching`, `require_type_kind`, `forbid_type_kind`, `required_name_suffix`, `required_name_prefix`, `forbidden_name_suffix`, `forbidden_name_prefix`, `require_type_name_matches_file_name`, `require_matching_interface`, `ignored_violations`, `reason`.
- [x] 2.4 Add `strict_layout_conventions` / `audit_layout_conventions` list properties to `ArchitectureContractGroups`.

## 3. Load-time validation

- [x] 3.1 Add `LayoutConventionsValidator : IArchitecturePolicyDocumentValidator` rejecting contracts with no usable `files_matching` selector field and contracts with no expectation field populated, matching `TypePlacementValidator`'s message style.
- [x] 3.2 Register `LayoutConventionsValidator` in `ArchitecturePolicyDocumentValidatorPipeline`.
- [x] 3.3 Extend `ExpressionCompilationValidator` (or add an equivalent pass) to compile `files_matching.when` at the two new locations against the existing `subject` `CelEnvironment` from `Contracts/Expressions/ArchitectureExpressionSchemas.cs`, fail-closed on any compile diagnostic, caching the result on `CompiledWhen`.

## 4. Diagnostics and payload

- [x] 4.1 Add `ArchitectureDiagnosticKind.LayoutConvention` to `Model/ArchitectureDiagnosticKind.cs`.
- [x] 4.2 Add `Model/LayoutConventionDiagnostic.cs` (mirrors `TypePlacementDiagnostic`) with fields for matched file path, expected/actual type kind, expected/actual name, expected counterpart name, and requiring source declaration.
- [x] 4.3 Add `Model/LayoutConventionPayload.cs` (mirrors `TypePlacementPayload`) implementing `IArchitectureDiagnosticPayload`.

## 5. Execution logic

- [x] 5.1 Add `Execution/ArchitectureAnalysisSession.LayoutConventions.cs` with `CheckLayoutConventionsContract(ArchitectureLayoutConventionContract contract)`: build candidate file groups from `ArchitectureSourceFileFactIndex.AllFacts` filtered by `files_matching` (folder/namespace/file-name AND semantics), apply `when` per declared type (using the shared `subject` context-factory building blocks from `core-cel-integration`), then evaluate `require_type_kind`/`forbid_type_kind`/naming/`require_type_name_matches_file_name`/`require_matching_interface` expectations.
- [x] 5.2 Implement the run-level "zero source-enriched facts" detection: when a `layout_conventions` contract is declared and `AllFacts` has no fact with non-null `SourceFilePath`, emit exactly one unavailable-data diagnostic for that contract and skip further evaluation for it.
- [x] 5.3 Implement `require_matching_interface` lookup against `AllFacts` by `SimpleTypeName`/`TypeKind`, including the ambiguous-multiple-candidates case (report as unresolved, not an implicit pick).
- [x] 5.4 Wire `ignored_violations` handling through the existing `ArchitectureContractExecutionContext` (`IsIgnored`, `CollectUnmatchedIgnores`), matching `type_placement`'s pattern.
- [x] 5.5 Wire strict vs. audit dispatch (audit violations reported but not failing strict validation), matching `type_placement`'s `StrictTypePlacement`/`AuditTypePlacement` dispatch.

## 6. Wiring into shared infrastructure

- [x] 6.1 Register the new family in `ArchitectureContractFamilyRegistry` and `ArchitectureContractFamilyBindings`.
- [x] 6.2 Add `LayoutConventionDiagnostic` handling to `ArchitectureDiagnosticFormatter` (text/JSON) and `ArchitectureSarifFormatter`.
- [x] 6.3 Add layout convention contract awareness to `ArchitectureAnalysisSession.PolicyConsistency` and `CoverageValidator` (rule-input coverage), matching how `type_placement` is covered.
- [x] 6.4 Add layout convention contract support to `ArchitectureBaselineLoadingService`, `ArchitectureBaselineComparer`, and `ArchitectureBaselineModels`.
- [x] 6.5 Extend `schema/dependencies.arch.schema.json` with `strict_layout_conventions`/`audit_layout_conventions`/`files_matching`/`require_matching_interface` node definitions.

## 7. Tests and fixtures

- [x] 7.1 Add green + violating fixtures for a Services-folder-contains-concrete-services convention.
- [x] 7.2 Add green + violating fixtures for an Interfaces-folder-contains-service-interfaces convention.
- [x] 7.3 Add fixture for a class missing its matching interface (`require_matching_interface`).
- [x] 7.4 Add fixture for a file whose declared type name does not match its file name (`require_type_name_matches_file_name`).
- [x] 7.5 Add fixture for a concrete class inside an Interfaces-segment folder/namespace (`forbid_type_kind: class`).
- [x] 7.6 Add fixture for an interface inside a Services-segment folder/namespace (`forbid_type_kind: interface`).
- [x] 7.7 Add fixture for a run with zero source-enriched facts (no `source_roots`) exercising the unavailable-data diagnostic.
- [x] 7.8 Add load-time validator tests: missing selector field, missing expectation field, invalid `when` compilation, `when` rejected at a non-`files_matching` location.
- [x] 7.9 Add a `when`-refinement test narrowing matched declared types by `subject.role`/`subject.sourcePaths`.
- [x] 7.10 Add `ignored_violations` suppression + unmatched-ignore tracking tests.
- [x] 7.11 Add audit-mode test confirming an `audit_layout_conventions` violation does not fail strict validation.
- [x] 7.12 Add JSON diagnostics stability test (deterministic ordering) for layout convention violations.

## 8. Docs and spec sync

- [x] 8.1 Update user-facing docs where `type_placement_contracts` is documented to add the new `layout_conventions` family (only if contract surface is user-visible per AGENTS.md doc policy).
- [x] 8.2 Run `openspec validate --all` and resolve any issues.
- [x] 8.3 Run `rtk make fmt` and `rtk make acceptance`; fix any failures.
