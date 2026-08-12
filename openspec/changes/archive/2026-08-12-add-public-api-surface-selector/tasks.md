## 1. Selector model and schema

- [x] 1.1 Add `ArchitecturePublicApiSurfaceSelector` POCO (`name_suffix`, `name_prefix`, `namespace`, `layer`, `base_type`, `implements_interface`, `has_attribute`, `role`) to `src/ArchLinterNet.Core/Contracts/Families/PublicApiSurfaceContractFamily.cs`.
- [x] 1.2 Add nullable `SurfaceSelector` property (`surface_selector`) to `ArchitecturePublicApiSurfaceContract`.
- [x] 1.3 Verify exact schema version wiring via `ArchitecturePolicyEffectiveSchemaValidator` (`src/ArchLinterNet.Core/Contracts/PolicyImports/ArchitecturePolicyEffectiveSchemaValidator.cs`), then add a `surface_selector` definition to `schema/dependencies.arch.schema.json` under `$defs.publicApiSurfaceContract`, mirroring `$defs.typeMatcher`'s `minProperties: 1` plus the additional `role` property. Do not touch the fragment schema or pinned `schema/0.5.1`/`schema/0.6.1` snapshots.

## 2. Load-time validation

- [x] 2.1 In `src/ArchLinterNet.Core/Contracts/Validators/PublicApiSurfaceValidator.cs`, reject a declared `surface_selector` with every field empty (configuration error identifying the contract).

## 3. Matching

- [x] 3.1 Implement structural matching for `surface_selector` by delegating to `ArchitectureTypeRoleMatcher.Matches` (construct/adapt an `ArchitectureTypeMatcher` from the selector's structural fields).
- [x] 3.2 Implement role matching for `surface_selector.role` by delegating to `ArchitectureContextSelectorMatcher.MatchesLiteral` via `ArchitectureRoleIndex.TryGetRole` (`sourceDescriptor: null`).
- [x] 3.3 Combine structural and role matches with AND semantics across every populated field, consistent with `type_placement.types_matching`.

## 4. Enumeration filtering (the two shared call sites)

- [x] 4.1 Apply the selector filter in `PublicApiSurfaceChecker` (`src/ArchLinterNet.Core/Execution/Checkers/PublicApiSurfaceChecker.cs`) so strict/audit validation only enumerates selected types/members.
- [x] 4.2 Apply the same selector filter in `ArchitectureAnalysisSession.CapturePublicApiSurface` (`src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.PublicApiSurface.cs`) so capture/diff/update/migrate resolve the identical selected surface.
- [x] 4.3 Confirm `ArchLinterNet.Testing` requires no separate code change (it shares the same Core session/checker path) — add a regression test proving CLI and Testing resolve the same effective surface for a selector-bearing contract.

## 5. Zero-match fail-closed

- [x] 5.1 In the checker, detect a configured `surface_selector` that matches zero exported types across the contract's resolved assemblies and emit a violation (mirroring `PublicApiSurfaceChecker.UnusableSnapshotViolation`'s "recorded, not thrown" shape) instead of throwing at policy load.
- [x] 5.2 Apply the same zero-match detection on the capture/diff/update/migrate path so those operations fail rather than silently produce a near-empty snapshot.

## 6. First-party signature escape

- [x] 6.1 Extend the internal `ArchitectureExportedApiEntry` record (`src/ArchLinterNet.Core/Scanning/ArchitecturePublicApiSurfaceScanner.cs`) to carry the set of type full names each member's signature references, reusing the scanner's existing recursive type-rendering walk.
- [x] 6.2 In `PublicApiSurfaceChecker`, for each selected entry, fail closed when a referenced type is declared in the contract's own `assemblies` (first-party) but is not itself in the selected set. Do not require selection evidence for BCL/external referenced types.
- [x] 6.3 Add a new violation shape/payload field distinguishing this "unselected first-party dependency" case from the existing undeclared-surface and forbidden-constant violations, surfaced consistently in human, JSON, and SARIF output.

## 7. Semantic role preservation

- [x] 7.1 Add a regression test proving a type selected via `has_attribute` (or another structural matcher) retains its existing winning semantic role (e.g. `ValueObject`, `Entity`) and continues to participate in existing semantic/contextual rules unchanged.
- [x] 7.2 Add a regression test proving a type whose genuine primary role is `ApiContract` can be selected via `surface_selector.role` without a separate mapping step.

## 8. Test coverage for the 12 required validation scenarios

- [x] 8.1 Orthogonal attribute marker selects a small surface while selected types retain non-`ApiContract` roles.
- [x] 8.2 `has_attribute` selection requires no role mapping.
- [x] 8.3 Semantic role selector path (`role: ApiContract`).
- [x] 8.4 At least one non-attribute structural matcher (interface, base type, or namespace) proven end to end.
- [x] 8.5 Exact API delta (`api_comparison: exact`) still detects additions/removals/signature changes within the selected surface.
- [x] 8.6 Adding/removing selector evidence produces a deterministic reviewed membership addition/removal.
- [x] 8.7 Zero-match selector fails closed (strict validation and capture/diff/update/migrate).
- [x] 8.8 First-party signature escape fails closed.
- [x] 8.9 BCL/external referenced types never require local API-membership evidence.
- [x] 8.10 Backward compatibility: a policy with no `surface_selector` behaves identically to today.
- [x] 8.11 CLI/Testing parity for a selector-bearing contract.
- [x] 8.12 Large synthetic modular-consumer fixture: whole-assembly snapshot shrinks to an intentional selected snapshot with no CLR-visibility or semantic-role changes.

## 9. Docs and spec synchronization

- [x] 9.1 Update `docs/contracts/public-api-surface.md` to document `surface_selector`, its fields, and the role-vs-membership distinction.
- [x] 9.2 Synchronize `openspec/specs/public-api-surface-contracts/spec.md` and `openspec/specs/public-api-snapshots/spec.md` with implemented behavior; run `openspec validate --all`.
- [x] 9.3 Run `openspec archive add-public-api-surface-selector` after implementation and doc sync are complete, before opening the PR.

## 10. Validation

- [x] 10.1 Run focused/cross-cutting risk-based local validation per `docs/ai/feature-implementation-workflow.md` (focused Core tests, `make fmt`, `make lint-architecture`, `openspec validate --all`); expand to the full Core test project since this is a cross-cutting shared-infrastructure change.
- [ ] 10.2 Open the PR closing #525, stating exactly which validation ran locally and what is delegated to CI.
