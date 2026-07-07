# Interface Implementation Contracts

Interface implementation contracts restrict where selected interfaces may be implemented: application ports implemented only by infrastructure/adapters, infrastructure abstractions never implemented by domain types, UI layers never implementing domain internals directly. This is a static type-relationship check on the compiled interface set, not runtime dependency-injection resolution.

Groups:

- `strict_interface_implementation`
- `audit_interface_implementation`

## Example

```yaml
contracts:
  strict_interface_implementation:
    - id: ports-implemented-only-by-adapters
      name: application-ports-implemented-only-by-adapters
      interface_prefixes: [MyApp.Application.Ports.]
      allowed_only_in_layers: [infrastructure]
      reason: Port implementations belong to the infrastructure boundary.
```

## When to use

Use interface implementation contracts when an interface's *implementations* should be confined to (or excluded from) a specific part of the codebase:

- application ports (`MyApp.Application.Ports.*`) may be implemented only in infrastructure/adapter layers;
- infrastructure abstractions must not be implemented by domain types;
- presentation/UI types should not implement domain-internal interfaces directly.

For forbidding *class inheritance* from framework base types, use [inheritance contracts](inheritance.md) instead.

## Semantics

### Selecting interfaces

`interfaces` is a list of exact, fully-qualified interface names matched by ordinal equality. `interface_prefixes` is a list of namespace/type-name prefixes matched by ordinal `StartsWith` — use it to select every port in a namespace without listing each one.

A contract must declare at least one of `interfaces` or `interface_prefixes`. A contract with neither is rejected at policy load time.

A non-interface type (class or struct) matches when **any** interface in its full interface set matches a selector — including interfaces implemented indirectly through a base class (a subclass of an adapter that implements a port is itself an implementation of that port). Constructed generic interfaces are matched by their generic type definition's CLR name (e.g. `` MyApp.Application.Ports.IRepository`1 ``). Each distinct matched interface produces at most one match per type.

Interface types themselves are never candidates: an interface *extending* a selected interface is a contract extension, not an implementation escaping the boundary.

### Location expectations

`allowed_only_in_layers`, `allowed_only_in_namespaces`, `allowed_only_in_projects`, and `allowed_only_in_assemblies` together form an allow-list: a matched implementation whose type's location does not satisfy **at least one** entry across all four lists is a **misplaced** violation.

`forbidden_in_layers`, `forbidden_in_namespaces`, `forbidden_in_projects`, and `forbidden_in_assemblies` together form a deny-list: a matched implementation whose type's location satisfies **any** entry across all four lists is a **forbidden** violation.

A contract must declare at least one allow-list entry or at least one deny-list entry; a contract with neither is rejected at policy load time.

If a single matched implementation fails both checks, exactly **one** violation is reported, described as `forbidden` (the more specific rule).

`allowed_only_in_projects`/`forbidden_in_projects` resolve each configured project name to its assembly name via project discovery — the same assembly-name-equivalence semantics documented for `type_placement`'s `must_reside_in_projects`.

### Violations

Each violation identifies the implementing type, the matched interface's fully-qualified name, whether the violation is `misplaced` or `forbidden`, the actual location, and — for `misplaced` violations only — the expected (allow-list) location description. Violations are emitted deterministically: implementing types ordered by fully-qualified name, matched interfaces within a type ordered by name (both ordinal).

`ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, matching the implementing type and the matched interface's fully-qualified name.

## Non-goals

- **Not runtime DI resolution.** The contract sees which types implement which interfaces in compiled metadata; it does not know which implementation a container actually resolves.
- No required-implementation rules ("interface X must have at least one implementation in layer Y").
- No regex or expression-language matching — only exact fully-qualified names and prefix matching.
