# Design: inheritance-implementation-contracts

## Context

The linter has 20+ contract families sharing a uniform wiring pattern, most recently `attribute_usage` (#192). Type-relationship primitives already exist: `ArchitectureTypeRoleMatcher` walks base-type chains and implemented interfaces (with defensive reflection) for `type_placement` selectors, and `ArchitectureAnalysisSession.AttributeUsage`/`TypePlacement` establish the allowed-only/forbidden location evaluation (`IsAllowedLocation`, `ResolveProjectAssemblyNames`) and deterministic ordering conventions. Issue #87 asks for first-class inheritance and implementation boundary families.

## Goals / Non-Goals

**Goals:**
- `strict_inheritance` / `audit_inheritance`: forbid inheritance from selected base types in selected source surfaces (layers, namespace prefixes).
- `strict_interface_implementation` / `audit_interface_implementation`: restrict where selected interfaces may be implemented (allowed-only and forbidden lists over layers/namespaces/projects/assemblies).
- Deterministic diagnostics naming the violating type, matched base type/interface, rule, and expected boundary.
- Full parity with existing families: `id` selection, `reason`, `ignored_violations`, unmatched-ignore tracking, policy consistency, strict/audit split, schema + capabilities + docs + coverage-report awareness.

**Non-Goals:**
- Runtime DI resolution, data-flow analysis, refactoring suggestions, OO design scoring (issue non-goals).
- Required-implementation rules ("interface X must have at least one implementation").
- Unrestricted expression-language selectors.

## Decisions

1. **Two orthogonal families, not one combined family.** Inheritance rules are source-surface-centric ("types in domain must not inherit X"); implementation rules are interface-centric ("implementations of X must live in Y"). Combining them would force awkward optional-field validation. The issue's YAML sketch (`strict_inheritance` with `source_layers` + `forbidden_base_types`) maps to family 1; "ports implemented only by adapters" and "domain must not implement infra interfaces" map to family 2's `allowed_only_in_*` / `forbidden_in_*`.

2. **Inheritance contract shape** (`ArchitectureInheritanceContract`): `name`, `id`, `source_layers`, `source_namespaces` (prefix-matched), `forbidden_base_types` (exact FQN), `forbidden_base_type_prefixes`, `ignored_violations`, `reason`. Matching walks the full base-class chain (transitive), so `class A : FrameworkBase` and `class B : A` both violate. Interfaces are NOT matched by this family — that is family 2's job.

3. **Interface implementation contract shape** (`ArchitectureInterfaceImplementationContract`): `name`, `id`, `interfaces` (exact FQN), `interface_prefixes`, `allowed_only_in_layers/namespaces/projects/assemblies`, `forbidden_in_layers/namespaces/projects/assemblies`, `ignored_violations`, `reason`. Matching uses `Type.GetInterfaces()` (includes inherited interfaces). Only concrete/abstract classes and structs are checked; interface types themselves are skipped (an interface extending a port is not an implementation escaping the boundary).

4. **Generic and nested type handling.** Base types and interfaces are compared via the generic type definition's full name when the runtime type is a constructed generic (`Repository<Order>` matches `forbidden_base_types: [App.Repository]`... actually `Ns.Repository\`1` — the raw CLR name with arity backtick is what `SafeFullName` yields for the definition; docs and diagnostics use CLR names, consistent with how `ArchitectureTypeNames` already renders names elsewhere). Prefix matching therefore also works on the CLR name. Nested types use the existing `SafeFullName` rendering. Compiler-generated and non-matchable chains fall back to the defensive-reflection posture of `ArchitectureTypeRoleMatcher` (`TypeLoadException`/`FileNotFoundException` treated as no-match).

5. **Fail-closed loader validation.** Inheritance contracts require ≥1 source selector (`source_layers`/`source_namespaces`) and ≥1 base-type selector; interface implementation contracts require ≥1 interface selector and ≥1 location expectation. Mirrors `attribute_usage` loader validation so schema-valid-but-empty contracts cannot silently pass everything.

6. **Violation/diagnostic model.** Extend `ArchitectureViolation` with optional fields (`ForbiddenBaseType`, `MatchedInterface`, `ImplementationBoundaryKind` misplaced/forbidden, `ExpectedImplementationLocation`, `ActualImplementationLocation`); add `InheritanceDiagnostic` and `InterfaceImplementationDiagnostic` records plus `ArchitectureDiagnosticKind.Inheritance` / `.InterfaceImplementation`; formatter/mapper handle both. Determinism: types ordered by `SafeFullName` ordinal, matched base types/interfaces ordered ordinal, one violation per (type, matched base/interface) pair; a match failing both allow and deny lists yields one "forbidden" violation (attribute-usage precedent).

7. **Catalog order:** the two new families are appended after `attribute_usage` and before `coverage` reserved families? No — appended after `attribute_usage` (before `strict_coverage`) in `ArchitectureContractCatalog.Build`, matching the "new families appended at the end of executable families" convention so existing output ordering is unchanged.

## Risks / Trade-offs

- [CLR generic names (`` `1 ``) in policy YAML may surprise authors] → docs show concrete examples for generic base types/interfaces; prefix matching gives an arity-free escape hatch.
- [Reflection-only base chain: base types in unresolvable assemblies stop the walk] → same accepted posture as `type_placement` `base_type` matcher; documented.
- [`interfaces` matching includes inherited interfaces, so an adapter base class implementing a port makes derived types match too] → intended (they are implementations); deduplicated per type+interface so diagnostics stay stable.

## Open Questions

(none — boundary vocabulary reuses the established `allowed_only_in_*`/`forbidden_in_*` and `source_layers` terms already shipped by attribute-usage/dependency families)
