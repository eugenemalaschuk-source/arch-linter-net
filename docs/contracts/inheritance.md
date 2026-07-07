# Inheritance Contracts

Inheritance contracts forbid types in selected source layers/namespaces from inheriting — directly or transitively — from selected base types (framework base classes such as `UnityEngine.MonoBehaviour` or `Microsoft.EntityFrameworkCore.DbContext`, or first-party base types that should stay in boundary layers). This is a static type-relationship check on the compiled base-class chain, not runtime behavior validation.

Groups:

- `strict_inheritance`
- `audit_inheritance`

## Example

```yaml
contracts:
  strict_inheritance:
    - id: domain-no-framework-base-types
      name: domain-must-not-inherit-framework-types
      source_layers: [domain]
      forbidden_base_types:
        - UnityEngine.MonoBehaviour
        - Microsoft.EntityFrameworkCore.DbContext
      reason: Domain types must stay framework-independent.
```

## When to use

Use inheritance contracts when a framework or boundary base class must not leak into a protected architecture surface:

- domain types must not inherit from `UnityEngine.MonoBehaviour`, `DbContext`, or ASP.NET base types (`ControllerBase`, `PageModel`, ...);
- framework-specific base classes should remain in boundary layers (UI, infrastructure, adapters);
- a deprecated first-party base class must not gain new subclasses outside a sanctioned area.

For restricting where *interfaces* may be implemented, use [interface implementation contracts](interface-implementation.md) instead — this family only checks the base-class chain.

## Semantics

### Selecting the source surface

`source_layers` is a list of declared layer names; a type is in the surface when its namespace resolves into one of those layers. `source_namespaces` is a list of namespace prefixes matched against the type's namespace. A type matching **any** entry across both lists is checked.

A contract must declare at least one of `source_layers` or `source_namespaces`. A contract with neither is rejected at policy load time — an empty source surface would silently check nothing.

### Selecting forbidden base types

`forbidden_base_types` is a list of exact, fully-qualified base type names matched by ordinal equality. `forbidden_base_type_prefixes` is a list of namespace/type-name prefixes matched by ordinal `StartsWith` (e.g. `UnityEngine.` forbids every UnityEngine base class at once).

A contract must declare at least one of `forbidden_base_types` or `forbidden_base_type_prefixes`.

The **full base-class chain** is walked: `class OrderView : ViewBase` violates a rule forbidding `FrameworkView` when `ViewBase` itself derives from `FrameworkView`. Constructed generic base types are matched by their generic type definition's CLR name — `class Orders : Repository<Order>` matches `` forbidden_base_types: [App.Data.Repository`1] `` (note the CLR arity suffix), and prefix entries match the same normalized name. Nested types are checked like any other type (reported as `Namespace.Outer+Nested`).

Interface implementations are **not** matched by this family — implementing an interface whose name appears in `forbidden_base_types` is not an inheritance violation.

### Violations

Each violation identifies the violating type, the matched forbidden base type's fully-qualified name, and the declared source surface. Violations are emitted deterministically: types ordered by fully-qualified name, matched base types within a type ordered by name (both ordinal), with at most one violation per (type, matched base type) pair.

`ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, matching the violating type and the matched base type name.

## Non-goals

- No required-inheritance rules ("every X must derive from Y").
- No automatic refactoring of inheritance hierarchies and no object-oriented design scoring.
- Base types in assemblies that cannot be resolved stop the chain walk at the last resolvable type (same defensive-reflection posture as `type_placement`'s `base_type` selector).
