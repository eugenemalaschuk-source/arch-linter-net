## Why

Semantic role discovery, selector-backed layers, contextual dependency contracts, and CEL
predicates can express many architecture rules over type facts and dependency edges — but
layout conventions such as "Services folders contain concrete service classes" or "a service
class must have a matching interface" are not dependency rules. They require deterministic
validation over indexed source files and declared types, which only became possible once
#171 (source-file-fact-index) landed. Without this capability, the semantic layout examples
in #169 remain documentation-only aspirations instead of enforceable contracts.

## What Changes

- Add a new `layout_conventions` contract family (`contracts.strict_layout_conventions` /
  `contracts.audit_layout_conventions`) that selects source files by folder segment,
  namespace segment, and file-name prefix/suffix (via the existing `ArchitectureSourceFileFactIndex`
  from #171), then validates the declared types in each matched file against type-kind,
  naming, file/type-name-matching, and matching-interface-counterpart expectations.
- Add a new `ArchitectureLayoutFileMatcher` selector (`files_matching`) with an optional
  `when` CEL predicate, reusing the existing `subject` CEL context schema unchanged
  (no new CEL schema members).
- Extend `cel-policy-model`'s closed first-wave `when` location list with two new entries
  for `files_matching.when` on the strict/audit layout contract families.
- Add deterministic diagnostics (`LayoutConventionDiagnostic`/`LayoutConventionPayload`,
  new `ArchitectureDiagnosticKind.LayoutConvention`) identifying the matched file, expected
  vs. actual type kind/name, and — for missing-counterpart violations — the expected
  counterpart type name and the source declaration that required it.
- Add a deterministic "path-based layout checks unavailable" diagnostic when a
  `layout_conventions` contract is declared but the run has no source-enriched declared-type
  facts at all (e.g. `source_roots` not configured), rather than silently reporting zero
  violations.
- Wire the new family through every existing extension point `type_placement_contracts`
  already uses: family registry/bindings, diagnostic/SARIF formatters, policy consistency
  and coverage validators, baseline loading/comparison, and the CLI's JSON policy schema.

## Capabilities

### New Capabilities
- `layout-convention-contracts`: schema-backed layout convention contracts that select
  source files by folder/namespace/file-name pattern and validate declared-type kind,
  naming, file/type-name correspondence, and matching-interface counterparts, with optional
  CEL `when` refinement and deterministic unavailable-data diagnostics.

### Modified Capabilities
- `cel-policy-model`: the closed first-wave `when` expression location list gains two new
  entries (`files_matching.when` under `strict_layout_conventions[*]` and
  `audit_layout_conventions[*]`), compiling against the existing `subject` context shape
  unchanged.

## Impact

- New files under `src/ArchLinterNet.Core/Contracts/Families/`, `Contracts/Validators/`,
  `Contracts/Expressions/` (reuse only), `Execution/`, and `Model/` for the new contract
  family, selector, validator, session logic, and diagnostics.
- Modifications to `ArchitectureContractFamilyRegistry`, `ArchitectureContractFamilyBindings`,
  `ArchitectureDiagnosticFormatter`, `ArchitectureSarifFormatter`,
  `ArchitectureAnalysisSession.PolicyConsistency`, `CoverageValidator`,
  `ArchitectureBaselineLoadingService`/`Comparer`/`Models`,
  `ArchitecturePolicyDocumentValidatorPipeline`, and `schema/dependencies.arch.schema.json`.
- New test fixtures and tests under `tests/ArchLinterNet.Core.Tests/`.
- No breaking changes — purely additive; existing policies without a `layout_conventions`
  section are unaffected.
