## Context

ArchLinterNet already has two reflection-based "role/marker" contract families that establish the pattern this change follows:
- `type_placement` — selects types by matcher and validates layer/namespace/project/assembly residency and naming.
- `public_api_surface` — enumerates exported (public/protected) types and members and validates against a declared signature allowlist.

Issue #86 asks for a third family that governs *attribute usage*: where specific `[Attribute]` markers on types/members are allowed or forbidden to appear. Unlike `public_api_surface`, this scan must not filter by visibility, because the attributes of interest (`[SerializeField]`, `[Authorize]`, `[Route]`, custom Unity/ASP.NET markers) commonly decorate private or internal members.

## Goals / Non-Goals

**Goals:**
- Declaratively restrict where specific attribute types may or may not appear, by layer/namespace/project/assembly, using the same allow-list/deny-list vocabulary already established by `type_placement`'s `must_reside_in_*` fields (renamed here to `allowed_only_in_*` per the issue's example YAML) plus a new `forbidden_in_*` deny-list.
- Scan every declared member (regardless of visibility) plus the type itself.
- Support both exact fully-qualified attribute-type-name matching (`attributes`) and prefix matching (`attribute_prefixes`), matching the existing `namespace_prefixes`/`type_prefixes` idiom used by external-dependency contracts.
- Emit one violation per (member, matched-attribute) pair, so a member with two matching attributes yields two violations.

**Non-Goals:**
- This is NOT a runtime authorization/security correctness validator. It does not know whether `[Authorize(Roles = "Admin")]` is semantically correct — it only knows an attribute type appeared somewhere it shouldn't (or didn't appear somewhere required, which is also out of scope — see below).
- Required-marker checks (e.g. "every controller action must declare `[Authorize]` or `[AllowAnonymous]`") are explicitly deferred to a documented follow-up. This version only validates placement of markers that *do* appear; it does not detect *absence* of a required marker.
- No new Roslyn compilation plumbing — reflection-only, consistent with `type_placement`/`public_api_surface`.

## Decisions

- **Reuse `IsAllowedLocation`**: `ArchitectureAnalysisSession.TypePlacement.cs` already has a private static `IsAllowedLocation(actualNamespace, actualAssemblyName, layers, namespacePrefixes, assemblyNames)` helper. Since `CheckAttributeUsageContract` lives in another partial of the same class, it can call this directly for both the allow-list check and (by construction) the deny-list check: a "forbidden" violation is exactly `IsAllowedLocation(..., forbiddenLayers, ForbiddenInNamespaces, forbiddenAssemblyNames) == true`.
- **Location is always derived from the enclosing type**, not the member, since attributes on a method/property/field don't have their own namespace/assembly — they inherit their enclosing type's.
- **No visibility filtering**: the new `ArchitectureAttributeUsageScanner` enumerates `BindingFlags.DeclaredOnly | Public | NonPublic | Instance | Static` for every member kind, skipping only compiler-synthesized property/event accessor methods (same `IsSpecialName` + `get_`/`set_`/`add_`/`remove_` convention as `ArchitecturePublicApiSurfaceScanner`) — never skipping based on `IsPublic`/`IsFamily`.
- **One violation per match, preferring "forbidden" when both fire**: if a contract declares both `allowed_only_in_*` and `forbidden_in_*` and a single match fails both checks, we emit exactly one violation describing it as `forbidden` (the more specific/severe rule), not two.
- **Defensive reflection**: catch `TypeLoadException`/`FileNotFoundException`/`CustomAttributeFormatException` around `GetCustomAttributesData()` calls, skip-and-continue, consistent with every other reflection scanner in this codebase.

## Risks / Trade-offs

- [Risk] A policy author might expect this family to also enforce "required markers," given how similar the YAML looks to `type_placement`. → Mitigation: explicit Non-goals section in `docs/contracts/attribute-usage.md` and in this design doc; the loader also plainly requires a location expectation so an accidental required-marker-shaped policy fails loudly rather than doing nothing silently.
- [Risk] Attribute matching by exact fully-qualified name is brittle across attribute assembly versions if the same attribute type is loaded from multiple assembly versions side-by-side. → Mitigation: matching is against `Type.FullName`, not assembly-qualified name, consistent with how `has_attribute` already works in `type_placement`; this is a known, accepted limitation shared with the existing matcher.
- [Risk] Scanning every member on every type without visibility filtering is more expensive than the public-API-surface scan. → Mitigation: still `DeclaredOnly`, and the existing `TypeIndex.AllTypes()` cache is reused; no new full-assembly enumeration pass is introduced.
