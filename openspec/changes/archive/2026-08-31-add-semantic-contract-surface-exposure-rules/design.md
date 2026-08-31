## Context

`ArchitectureContractSurfaceExposureIndex` from #512 already provides session-cached, deterministic paths from caller-selected roots through exported visible signatures and compiled metadata. `public-api-surface` from #525 already materializes intentional API membership with the existing structural matcher and semantic-role index. The missing work is one policy family that joins those two facts, projects applicability through #507, and reuses the standard finding lifecycle.

## Goals / Non-Goals

**Goals:**

- Add a bounded declarative exposure-contract family whose sources and forbidden targets use existing type, role, project, assembly, and reviewed-API selection evidence.
- Reuse #512 evidence unchanged and preserve the selected API member's existing primary role.
- Make both detectable leaks and insufficient evidence deterministic across strict/audit and all normalized output consumers.

**Non-Goals:**

- Add runtime serialization, data-flow, endpoint, DTO-generation, or arbitrary-expression analysis.
- Change public-API snapshot syntax/lifecycle, semantic classification, type-placement, attribute-placement, or inheritance semantics.
- Implement versioned contract isolation (#514) or realistic end-to-end reference policies (#515).

## Decisions

### 1. Add one explicit family with bounded source and target selector shapes

Add `strict_contract_surface_exposure` and `audit_contract_surface_exposure` groups containing `ArchitectureContractSurfaceExposureContract`. Every contract requires a stable `id`, a `source`, and a non-empty `forbidden` list.

`source` supports four conjunctive filters: explicit `assemblies`, explicit `projects`, an optional bounded type selector, and an optional `public_api_surface` contract identity. Its type selector reuses the same eight fields and matcher behavior as `public-api-surface.surface_selector` (`name_suffix`, `name_prefix`, `namespace`, `layer`, `base_type`, `implements_interface`, `has_attribute`, and `role`). `forbidden` is a non-empty list of the same bounded type-selector shape: fields inside one selector AND-combine and selectors in the list OR-combine. An empty selector is invalid in either location.

Projects and assemblies are source-only because the issue requires location selection for the visible source surface; target selection deliberately stays within the existing semantic/structural type vocabulary. Direct roots are restricted to the exported visible shape used by #512 and #525. A selected internal type is not silently treated as a new contract surface; if no exported source remains, the control is unassessable.

Alternative considered: a new generic CEL selector or a multi-role API tag. Rejected because existing bounded matchers already express the required criteria and the issue explicitly preserves the single-role model.

### 2. Resolve reviewed API roots through the existing effective-surface seam

Add one narrow Core-internal resolver on the public-API surface analysis seam that returns the effective selected exported roots for a referenced public-API contract. It uses the existing public-API materialization and selector predicate, not snapshot contents or duplicated reflection matching. The exposure checker receives that fact through `ArchitectureCheckerContext`; it does not reach into session lifecycle state.

When a `source.public_api_surface` is populated, those roots are the source universe and any other populated source filters narrow it conjunctively. This supports an intentional API surface selected by an orthogonal marker while retaining its original role.

Alternative considered: reconstruct selection from `api_snapshot` signatures. Rejected because a snapshot is a reviewed compatibility artifact, not membership authority, and it would make capture/diff state affect policy selection.

### 3. Reuse the exposure index and keep diagnostics typed

The checker resolves deterministic selected source roots and target types, then requests `ArchitectureContractSurfaceExposureIndex` with `ArchitectureContractSurfaceShape.Exported`. It matches each referenced target with the existing selector matcher and emits a `ContractSurfaceExposurePayload` containing typed source-surface facts, the declaring root, path tokens/readable path, member-or-metadata site, and target assembly/full type identity. Ordering is source identity, path canonical key, target assembly, then target type.

The standard violation's source is the declaring root and forbidden reference is the target identity. Ignore matching receives the target assembly/type and the path/member facts. Baseline identity adds source assembly/type, source member/site, canonical exposure path, target assembly, and target type, so separate paths and same-named cross-assembly targets never collapse.

Alternative considered: flattening the leak into an ordinary reference-graph edge. Rejected because it loses the recursive visible-contract path and metadata-site distinction required by the issue.

### 4. Add family applicability through the standard handler result boundary

Extend `ArchitectureHandlerResult` and standard-family execution to carry optional applicability expected entries and records, rather than special-casing this family in the executor. The exposure checker returns exactly one required expected entry and one record for each effective contract identity.

The record is `evaluable` only when all configured source and forbidden selectors produce complete required evidence and #512 reports no incomplete records for the selected roots. A direct source selector, referenced API surface, or forbidden selector matching zero subjects produces `unassessable` with `unexpected_empty_input` or `stale_declaration` provenance. Incomplete visible signature/metadata evidence produces `unassessable` with `missing_required_input`. The family uses existing canonical applicability projection and adds no exposure-specific result envelope.

Alternative considered: reporting zero matches as ordinary violations. Rejected because #506 requires valid-but-insufficient evidence to remain distinguishable from a trusted evaluated architecture failure.

### 5. Keep validation fail-closed at both raw YAML and typed-policy layers

Add raw-key validation for the new groups so unknown YAML fields cannot disappear through deserialization. Add typed validation for a non-empty id, bounded/non-empty source and forbidden selectors, known source assembly names, referenced reviewed-API contract identity, no self-reference, and valid project/selector list values. Policies that are structurally invalid retain the current invalid-policy path; runtime resolution gaps use applicability evidence.

### 6. Register every existing lifecycle touchpoint deliberately

The family is added to groups aggregation, family bindings/registry, catalog/handler dispatch, policy consistency and source-configuration collection where applicable, baseline group inventory/loading/comparison, identity descriptors, raw and typed validators, schema definitions, and normalized payload projection. Tests pin strict/audit behavior, canonical identity/baseline, Human/JSON/SARIF/Testing parity, source/target selection and zero-match/incomplete applicability, plus direct/nested/metadata leakage.

## Risks / Trade-offs

- **A new source resolver drifts from reviewed API selection** → route every referenced surface through existing public-API materialization and selector matching; add a parity fixture with an orthogonally selected value object.
- **Selector or reflection gaps falsely look clean** → require one applicability record per contract and downgrade unresolved/zero-match evidence to typed unassessable state.
- **Path-rich findings create duplicate debt identities** → use typed canonical path and target dimensions, not prose, and pin distinct-path/same-name fixtures.
- **Policy shape accidentally becomes an open expression language** → raw/schema/typed validation admit only the established bounded selector fields.

## Migration Plan

The new groups are additive; policies without them preserve existing behavior. Adopters first add a contract with an explicit id, source, and forbidden selectors, then review any findings or baseline entries through the normal strict/audit lifecycle. Removing the contract removes only this new family; no snapshots, roles, or existing contracts require migration.
