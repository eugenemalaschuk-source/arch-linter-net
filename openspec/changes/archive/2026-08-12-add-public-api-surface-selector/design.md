## Context

`public-api-surface` contracts (`strict_public_api_surface`/`audit_public_api_surface`) govern every exported (public/protected) type in each configured assembly. The governed universe is computed at exactly two Core call sites, both delegating to the same unconditional scanner call:

- `PublicApiSurfaceChecker.ScanContractAssemblies` (strict/audit validation) — `src/ArchLinterNet.Core/Execution/Checkers/PublicApiSurfaceChecker.cs:217`
- `ArchitectureAnalysisSession.CapturePublicApiSurface` (capture/diff/update/migrate, reached from every CLI `public-api` subcommand via `ArchitecturePublicApiApplicationService.SurfaceResolution.cs`) — `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.PublicApiSurface.cs:31`

Both call `ArchitecturePublicApiSurfaceScanner.GetExportedSurface(assembly)` with no type-level filter. `ArchLinterNet.Testing` has no separate code path — it drives the same `ArchitectureEngine` → `ArchitectureAnalysisSession` → `PublicApiSurfaceChecker` pipeline the CLI `validate` command uses. This means filtering once, at these two call sites, automatically gives CLI/Testing/capture/diff/update parity with no Testing-specific changes.

Two existing, separately-matched selector engines already exist in Core and must be reused rather than forked:
- **Structural**: `ArchitectureTypeMatcher` (`name_suffix`/`name_prefix`/`namespace`/`layer`/`base_type`/`implements_interface`/`has_attribute`) + `ArchitectureTypeRoleMatcher.Matches` (`src/ArchLinterNet.Core/Scanning/ArchitectureTypeRoleMatcher.cs`) — used today by `type_placement`.
- **Role**: `ArchitectureContextSelector` (`role`/`metadata`/`when`) + `ArchitectureContextSelectorMatcher.MatchesLiteral` reading `ArchitectureRoleIndex.TryGetRole` — used today by contextual/port-boundary contracts.

These two shapes are structurally disjoint today (the type matcher has no role/metadata; the context selector has no name/namespace/interface/base/attribute fields), and the issue requires supporting both selection styles on one new selector without inventing a third matching engine.

## Goals / Non-Goals

**Goals:**
- Optional, backward-compatible `surface_selector` on `public-api-surface` contracts.
- Reuse `ArchitectureTypeRoleMatcher` and `ArchitectureRoleIndex`/`ArchitectureContextSelectorMatcher` verbatim — zero new matching algorithms.
- One effective selected surface used identically by strict/audit validation, capture, diff, update, migrate, CLI, and Testing.
- Fail closed on a zero-match selector and on a first-party signature escape.
- Never touch or require changing a type's existing single winning semantic role.

**Non-Goals:**
- No `metadata` or `when`/CEL support on `surface_selector` — nothing required by the issue's acceptance criteria needs them, and the issue explicitly forbids a new unrestricted selector language.
- No selector lists / OR-of-selectors on one contract — a single object mirrors `types_matching`'s existing single-object convention; nothing in the issue's 12 required scenarios needs OR-of-selectors.
- No general recursive DTO/domain/persistence/framework/version exposure graph (v0.8 territory, #512/#513) — the first-party escape check is a flat one-hop "does this selected member reference an unselected first-party type," not a transitive graph walk.
- No changes to `type-placement-contracts` or `semantic-role-index` behavior — this change is a pure consumer of their existing read APIs.

## Decisions

### 1. Selector shape: one new POCO, structural fields + a `role` field, AND-combined

`ArchitecturePublicApiSurfaceSelector` carries the same 7 fields as `ArchitectureTypeMatcher` (so it can be matched by literally calling `ArchitectureTypeRoleMatcher.Matches` with an adapted/embedded matcher) plus a `role` string field (matched by constructing a transient `ArchitectureContextSelector { Role = selector.Role }` and calling `ArchitectureContextSelectorMatcher.MatchesLiteral(...)`, `sourceDescriptor: null` since API-surface selection has no "source" concept). Every populated field AND-combines — identical to how `type_placement.types_matching` already treats multiple populated fields, so there is no new combination semantics to document or test from scratch.

**Alternative considered**: reuse `ArchitectureTypeMatcher` directly and bolt a `Role` property onto it. Rejected — it would pollute `type_placement`'s own matcher type with a field meaningless to that family, and couples two contract families' schemas together for no benefit.

### 2. Single selector, not a list

`surface_selector` is one optional object, matching `types_matching`'s existing single-required-object convention on `type_placement`. A list-of-selectors OR-union is not required by any of the issue's 12 validation scenarios; adding it now would be speculative capability the issue doesn't ask for.

### 3. First-party escape scoped to the contract's own `assemblies`

"First-party" means declared in one of *this contract's* `assemblies` — the issue's literal wording is "an exported first-party type from the governed assembly set" — not the broader `analysis.target_assemblies` universe. This keeps the check local to data the two call sites already have (`resolvedAssemblies` filtered to `contract.Assemblies`), and correctly excludes BCL/external types by construction: a referenced type only becomes an escape candidate if it's actually declared by one of the contract's own assemblies. `ArchitectureExportedApiEntry` gains a set of referenced type full names, computed by the scanner's existing recursive type-rendering walk (the same logic that already renders parameter/return/field/property type names). The checker computes, per selected entry, whether any referenced first-party type name is absent from the selected type-name set; if so it emits a new violation. This runs only when a selector is configured — with no selector, every first-party type is selected by construction, so the check is vacuously satisfied.

**Alternative considered**: build a full transitive reachability graph (walk escape targets' own references recursively). Rejected as out of scope — that is v0.8's recursive exposure governance (#512/#513); this issue only needs one hop from a selected member to its immediately referenced types.

### 4. Zero-match selector fails closed at validation/capture time, not policy load

Selector resolution requires reflected/loaded assemblies, which are not available at policy-document-load time (only `document.Analysis.TargetAssemblies` string membership is checked at load, in `PublicApiSurfaceValidator`). The closest existing precedent *for this exact contract family* is `ApiSnapshotError`: a missing/unparsable/foreign snapshot is deliberately "recorded, not thrown" at load and turned into a violation later, specifically so an unrelated command against the same policy document (or the very `public-api capture` that would create the missing artifact) isn't blocked. A zero-match `surface_selector` follows the same shape — recorded and turned into a checker-level violation (mirroring `PublicApiSurfaceChecker.UnusableSnapshotViolation`) — applied on both the strict/audit path and the capture/diff/update/migrate path, so a capture can't silently write a near-empty snapshot with no signal.

**Alternative considered**: hard `throw` at policy load, mirroring `ArchitectureSourceSetExpander.ResolveDeclaration`'s zero-match throw for `source_sets`. Rejected — that pattern works there specifically because source-set resolution is glob/file-based and available without reflecting assemblies; `public-api-surface` already has an established, family-specific "defer past load" pattern for exactly this class of problem (`ApiSnapshotError`), and consistency with the closer, same-family precedent outweighs consistency with the more generic but timing-incompatible one.

### 5. Semantic roles stay read-only

The selector's `role` field only *reads* `ArchitectureRoleIndex.TryGetRole` for matching; nothing in this change writes to or mutates classification. Selecting a type via `has_attribute`/interface/base/namespace/etc. therefore cannot, even accidentally, change that type's existing winning role — enforced by construction (no new write path exists), verified by a regression test asserting a selected `ValueObject`/`Entity` type's role is unchanged pre/post selection.

## Risks / Trade-offs

- **[Risk]** Extending the internal `ArchitectureExportedApiEntry` record with referenced-type-name data touches a hot scanning path used by every `public-api-surface` contract, not just selector-bearing ones. → **Mitigation**: the additional data is computed from type information the scanner already reflects for signature rendering (no new reflection calls), and is only consumed by the checker when a selector is present; unselected contracts pay the same cost they always did for entry construction.
- **[Risk]** A selector that AND-combines a structural field and `role` could be confusing if a type matches structurally but has no resolved role at all (or vice versa) — `roleIndex.TryGetRole` returning false makes the whole AND fail, which might read as "the selector is broken" rather than "the type isn't classified." → **Mitigation**: this is identical to how `ArchitectureContextSelectorMatcher` already behaves for other families; no new failure mode, and existing diagnostic/explain tooling already surfaces "selector matched no classified type" for the role half.
- **[Risk]** Deferring zero-match to validation/capture time (not load) means a typo'd `has_attribute` value silently loads successfully and only surfaces when the contract actually runs. → **Mitigation**: this is consistent with how every other configuration mistake in this family already surfaces (missing snapshot, foreign snapshot ownership) — the checker-level violation is not optional, it fires on every strict/audit run and every capture/diff/update/migrate invocation, so it cannot be missed in practice.

## Migration Plan

No migration required — `surface_selector` is purely additive and optional. Existing policies are unaffected. Adopters migrate incrementally per contract: author a `surface_selector`, run `public-api capture` to produce a smaller reviewed snapshot, review the delta, replace the old whole-assembly snapshot. No flag or opt-in period is needed beyond authoring the field itself.

## Open Questions

None outstanding — the four design questions raised during exploration (selector shape, single-vs-list, first-party scope, zero-match timing) are settled above.
