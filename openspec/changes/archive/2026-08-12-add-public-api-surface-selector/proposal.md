## Why

`strict_public_api_surface`/`audit_public_api_surface` contracts today govern every exported (public/protected) type in each configured assembly unconditionally. A modular assembly with many CLR-public implementation/domain/configuration types that are not intended compatibility contracts is forced into a large, workaround-shaped whole-assembly snapshot just to satisfy the linter. This closes the gap identified by GitHub issue #525 (child of the v0.6.4 release story #527, gated by consumer-exit proof #526): let adopters define the intentional reviewed public-API surface using already-delivered bounded type-selection evidence (`type_placement`'s structural matchers, the semantic role index) instead of every exported type in the assembly, without forcing a second `ApiContract`-style reclassification of a type's single winning semantic role.

## What Changes

- Add an optional `surface_selector` field to `strict_public_api_surface`/`audit_public_api_surface` contracts. Absent (the default) preserves today's exact assembly-wide behavior — fully backward compatible.
- `surface_selector` supports the same 7 structural matcher fields already used by `type_placement.types_matching` (`name_suffix`, `name_prefix`, `namespace`, `layer`, `base_type`, `implements_interface`, `has_attribute`) plus a `role` field for selecting via existing semantic-role facts. All populated fields AND-combine, mirroring `types_matching`'s existing convention. No new matcher/tag/classification engine is introduced — matching delegates entirely to the existing `ArchitectureTypeRoleMatcher` and `ArchitectureRoleIndex`/`ArchitectureContextSelectorMatcher` implementations.
- A `surface_selector` with no populated field is a policy-load configuration error (same "at least one bounded criterion" invariant `type_placement`'s schema already enforces).
- Selector filtering applies identically at every place the effective exported surface is computed: strict/audit validation, `public-api capture`, `public-api diff`, `public-api update`, `public-api migrate`, and `ArchLinterNet.Testing` — all resolve one effective selected surface, because they all funnel through two shared Core call sites.
- A selector that resolves to zero governed types fails closed as a checker-level violation (reported at validation/capture time, not at policy load, mirroring the existing `ApiSnapshotError`/`UnusableSnapshotViolation` pattern already used by this contract family) — it cannot silently produce a false-green check or a silently near-empty capture.
- A selected member's signature that references an unselected, first-party exported type (declared in one of the contract's own `assemblies`) fails closed as a new violation, rather than silently hiding the dependency. Ordinary BCL/external referenced types never trigger this.
- Selecting a type via `surface_selector` never changes that type's existing single winning semantic role — role/classification stays read-only from this feature.
- Docs and schema updated to describe `surface_selector` and explicitly distinguish "semantic role" (the type's one winning architecture classification) from "API membership" (an orthogonal, optional compatibility-surface decision), so adopters don't reclassify a type merely to shrink a snapshot.

## Capabilities

### New Capabilities
(none — this extends existing capabilities rather than introducing a new one)

### Modified Capabilities
- `public-api-surface-contracts`: adds the optional `surface_selector` field, its structural/role matching semantics, the "at least one criterion" load-time validation, the zero-match fail-closed violation, and the first-party signature-escape fail-closed violation.
- `public-api-snapshots`: capture/diff/update/migrate must resolve the same selector-filtered effective surface as strict/audit validation, instead of always scanning every exported type in the contract's assemblies.

`type-placement-contracts` and `semantic-role-index` are unchanged — this change only *reads* their existing matcher/index APIs (`ArchitectureTypeRoleMatcher.Matches`, `ArchitectureRoleIndex.TryGetRole` via `ArchitectureContextSelectorMatcher.MatchesLiteral`); no delta spec is needed for either.

## Impact

- **Code**: `src/ArchLinterNet.Core/Contracts/Families/PublicApiSurfaceContractFamily.cs` (new selector POCO + contract field), `src/ArchLinterNet.Core/Contracts/Validators/PublicApiSurfaceValidator.cs` (load-time validation), `src/ArchLinterNet.Core/Execution/Checkers/PublicApiSurfaceChecker.cs` (selector filtering, zero-match violation, first-party escape violation), `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.PublicApiSurface.cs` (selector filtering on the capture path), `src/ArchLinterNet.Core/Scanning/ArchitecturePublicApiSurfaceScanner.cs` (extend `ArchitectureExportedApiEntry` with referenced-type names for the escape check).
- **Schema**: `schema/dependencies.arch.schema.json` gains a new selector definition on `$defs.publicApiSurfaceContract`.
- **Docs**: `docs/contracts/public-api-surface.md`.
- **Tests**: `tests/ArchLinterNet.Core.Tests` public-api-surface and type-placement fixtures; a large-modular-consumer regression scenario proving snapshot shrinkage without visibility/role changes.
- **No breaking changes** — the field is optional and every existing policy without it behaves exactly as before.
- This is implemented directly against issue #525. Its parent story is #527 (v0.6.4 adoption patch); the packed-artifact consumer-exit gate #526 depends on this landing first and is out of scope for this change.
