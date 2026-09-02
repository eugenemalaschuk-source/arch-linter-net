# Attribute Usage Contracts

Attribute usage contracts declare which layers/namespaces/projects/assemblies specific attribute types (ASP.NET routing/authorization attributes, Unity serialization attributes, custom markers, ...) are allowed to appear in, or forbidden from appearing in. This is static marker *placement* governance — it reports where a matching attribute was found, not whether the attribute's arguments are semantically correct and not whether a required marker is missing.

Groups:

- `strict_attribute_usage`
- `audit_attribute_usage`

## Example

```yaml
contracts:
  strict_attribute_usage:
    - id: aspnet-attributes-api-only
      name: aspnet-attributes-must-stay-in-api-layer
      attributes:
        - Microsoft.AspNetCore.Mvc.ApiControllerAttribute
        - Microsoft.AspNetCore.Mvc.RouteAttribute
      allowed_only_in_layers: [api]
      reason: ASP.NET attributes define API boundary concerns.
```

## Compose marker placement with boundary contracts

`attribute_usage` answers one deliberately low-level question: **where does a
selected marker appear?** It does not select the published API, and it does
not inspect what a public signature exposes. Compose it with the other
boundary families when those questions also matter:

| Question | Contract family |
| --- | --- |
| Is a marker present in an allowed or forbidden location? | `attribute_usage` |
| Which exported types and members are intentional API membership, and what snapshot records them? | `public_api_surface` + `surface_selector` |
| Does a selected contract expose a forbidden type through a recursive CLR-visible signature? | `contract_surface_exposure` |
| May one local version group expose another version or implementation group? | `versioned_contract_surface_isolation` |

For an established boundary, put the placement rule in `strict_attribute_usage`.
During migration, put a candidate restriction in `audit_attribute_usage` so
the same finding is discoverable without making it part of the current strict
gate. The fields below are a complete, declarative example of both choices:

```yaml
contracts:
  strict_attribute_usage:
    - id: orders-api-markers
      name: orders-api-markers-stay-at-the-api-boundary
      attributes:
        - Microsoft.AspNetCore.Mvc.RouteAttribute
        - Microsoft.AspNetCore.Mvc.ApiControllerAttribute
      allowed_only_in_namespaces:
        - Acme.Orders.Api
      reason: Routing and API-controller markers belong to the server API boundary.

  audit_attribute_usage:
    - id: orders-authorization-markers
      name: find-authorization-markers-outside-the-api
      attributes:
        - Microsoft.AspNetCore.Authorization.AuthorizeAttribute
      forbidden_in_namespaces:
        - Acme.Orders.Domain
        - Acme.Orders.Persistence
      reason: Discover authorization markers that remain on non-API types before enforcing the boundary.
```

The same composition works for a library/runtime-editor split. A private
Unity field is still in scope because attribute usage scans every visibility;
the enclosing type's assembly is what the location check uses:

```yaml
contracts:
  strict_attribute_usage:
    - id: game-runtime-serialization-markers
      name: unity-serialization-markers-stay-in-runtime
      attributes:
        - UnityEngine.SerializeField
      allowed_only_in_assemblies:
        - Acme.Game.Runtime
      reason: Unity serialization markers belong to runtime-facing game types.

  audit_attribute_usage:
    - id: game-editor-markers-in-runtime
      name: find-editor-markers-in-runtime
      attribute_prefixes:
        - UnityEditor.
      forbidden_in_assemblies:
        - Acme.Game.Runtime
      reason: Find editor-only markers that would couple runtime code to the editor during migration.
```

The marker used by `public_api_surface.surface_selector.has_attribute` can be
user-owned (for example, `Acme.Orders.Api.PublicApiContractAttribute`); it is
not a built-in ArchLinterNet marker. If that marker is also checked here, this
family only governs where it appears. It never overwrites a selected
`ValueObject`, `Entity`, `Controller`, or `Adapter` primary semantic role.
See [public API surface contracts](public-api-surface.md) for membership and
[contract-surface exposure contracts](contract-surface-exposure.md) for
recursive signature evidence.

## When to use

Use attribute usage contracts when a marker attribute's *presence* should be confined to (or excluded from) a specific part of the codebase:

- ASP.NET routing/MVC attributes (`[Route]`, `[ApiController]`, `[HttpGet]`) should only appear in the API layer.
- `[Authorize]`/`[AllowAnonymous]` should never appear in the domain layer.
- Unity's `[SerializeField]` should only appear on MonoBehaviour/ScriptableObject fields in a designated Unity-facing assembly, not in pure core/domain code.
- A custom `[Experimental]`/`[Obsolete]`-style marker should be confined to a sanctioned area of the codebase.

## Semantics

### Selecting attributes

`attributes` is a list of exact, fully-qualified attribute type names (e.g. `Microsoft.AspNetCore.Mvc.RouteAttribute`), matched by ordinal equality against the attribute type's full name.

`attribute_prefixes` is a list of namespace/type-name prefixes (e.g. `UnityEngine.`), matched by ordinal `StartsWith` against the attribute type's full name. Use this to match every attribute in a given namespace without listing each one individually.

A contract must declare at least one of `attributes` or `attribute_prefixes`. A contract with neither is rejected at policy load time — there is nothing to match against.

### Scanning: types and members, every visibility

For every loaded type, the contract scans the type itself and every declared member — constructors, methods (excluding compiler-generated property/event accessor methods), properties, fields, and events — for attributes matching the configured selectors. **Unlike `public_api_surface`, this scan does not filter by visibility.** Markers such as Unity's `[SerializeField]` commonly decorate `private` fields, and `[Authorize]`/`[Route]` can be declared on `internal` members; all of these are in scope.

A member decorated with two different matching attributes produces two separate matches (and, if either fails a location check, two separate violations) — one per matched attribute.

### Location expectations

`allowed_only_in_layers`, `allowed_only_in_namespaces`, `allowed_only_in_projects`, and `allowed_only_in_assemblies` together form an allow-list: a matched attribute usage whose enclosing type's location does not satisfy **at least one** entry across all four lists is a **misplaced** violation.

`forbidden_in_layers`, `forbidden_in_namespaces`, `forbidden_in_projects`, and `forbidden_in_assemblies` together form a deny-list: a matched attribute usage whose enclosing type's location satisfies **any** entry across all four lists is a **forbidden** violation.

A contract must declare at least one allow-list entry or at least one deny-list entry. A contract with neither is rejected at policy load time — it can never produce a violation.

`allowed_only_in_namespaces` and `forbidden_in_namespaces` entries accept the same constrained glob grammar as a layer's `namespace` (a literal namespace, or `*` as one or more complete segments — see [Namespace-allowance contract fields](../policy-format/layers-and-namespaces.md#namespace-allowance-contract-fields-use-the-same-grammar)). An entry using unsupported wildcard syntax is rejected at policy load with an actionable error instead of silently matching nothing.

Location (namespace/assembly) is always derived from the attribute's **enclosing type**, never the member itself — a method or field doesn't have its own namespace or assembly.

If a contract declares both an allow-list and a deny-list and a single matched attribute usage fails both checks, exactly **one** violation is reported, described as `forbidden` (the more specific rule) rather than two.

`allowed_only_in_projects`/`forbidden_in_projects` resolve each configured project name to its assembly name via project discovery, the same assembly-name-equivalence semantics documented for `type_placement`'s `must_reside_in_projects`.

### Violations

Each violation identifies the source member (or type), the matched attribute's fully-qualified name, whether the violation is `misplaced` or `forbidden`, the actual location, and — for `misplaced` violations only — the expected (allow-list) location description.

Constructor and method source identifiers include a parameter-type signature (e.g. `MyApp.Foo.Bar(System.Int32)`) so that two overloads of the same member — each possibly carrying different attributes — produce distinct, individually addressable identifiers instead of colliding on `Type.Method`.

Violations are emitted in a deterministic order: types are sorted by fully-qualified name, and matches within a type are sorted by source identifier, then by matched attribute name, both ordinal — independent of reflection metadata enumeration order, matching the ordering guarantee `type_placement` and `public_api_surface` already provide.

`ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, matching the source identifier and the matched attribute's fully-qualified name.

## Non-goals

- **Not a runtime authorization/security correctness validator.** This contract family only detects that an attribute type appears (or doesn't appear) somewhere; it does not evaluate constructor arguments or named properties (e.g. it cannot tell whether `[Authorize(Roles = "Admin")]` grants the right roles).
- **No required-marker checks.** A rule like "every controller action must carry `[Authorize]` or `[AllowAnonymous]`" — validating the *absence* of a required attribute — is explicitly **deferred to a documented follow-up** and is not implemented by this contract family. `attribute_usage` only validates the placement of markers that are actually present.
- No regex or expression-language attribute matching — only exact fully-qualified names and prefix matching.
- No IL/method-body scanning — attributes are discovered via reflection metadata (`GetCustomAttributesData()`), not by analyzing how a member is used.
- No API membership, reviewed snapshot, recursive contract-surface, or
  version-negotiation decision. For those concerns use
  [`public_api_surface`](public-api-surface.md),
  [`contract_surface_exposure`](contract-surface-exposure.md), and
  [`versioned_contract_surface_isolation`](versioned-contract-surface-isolation.md).
