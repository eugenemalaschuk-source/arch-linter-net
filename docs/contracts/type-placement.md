# Type Placement Contracts

Type placement contracts select architectural roles (controllers, handlers, domain events, Unity `MonoBehaviour`s, ...) by constrained matchers, and require the matched types to reside in a declared layer/namespace/project/assembly and/or carry a declared naming suffix/prefix. This is architectural placement and naming governance, not general code formatting or style enforcement.

Groups:

- `strict_type_placement`
- `audit_type_placement`

## Example

```yaml
layers:
  api:
    namespace: MyApp.Api

contracts:
  strict_type_placement:
    - id: controllers-in-api
      name: controllers-must-live-in-api-layer
      types_matching:
        name_suffix: Controller
      must_reside_in_layers: [api]
      required_name_suffix: Controller
      reason: Controller types are API boundary types and must be named and placed consistently.
```

## When to use

Use type placement contracts when an architectural role needs to be governed by *where it lives* and *how it's named*, not just what it may reference:

- controllers must live in the API/controller layer;
- command/query handlers must live in the application handler layer;
- domain events should carry a consistent naming suffix;
- infrastructure types (implementing a known interface or carrying a known attribute) must not appear in domain namespaces;
- Unity `MonoBehaviour`-derived types must not appear in pure core namespaces.

## Semantics

### Selecting types

`types_matching` selects candidate types using only these fields, all optional, combined with **AND** semantics (an unset/empty field is not applied):

- `name_suffix` / `name_prefix` — match the type's simple name (not namespace-qualified), ordinal.
- `namespace` — match the type's namespace by prefix (segment-aware, same convention as every other namespace matcher in this policy format).
- `layer` — match a declared layer name; the type's namespace must resolve to that layer.
- `base_type` — match a base type by its full name, walking the type's base-type chain (transitive).
- `implements_interface` — match an implemented interface by its full name.
- `has_attribute` — match a custom attribute applied to the type, by its full name.

There is no regex or expression-language selector. Every matcher is an exact/prefix/suffix/full-name string comparison.

### Placement expectations

`must_reside_in_layers`, `must_reside_in_namespaces`, `must_reside_in_projects`, and `must_reside_in_assemblies` are all optional lists that together form a single set of allowed locations: a selected type's actual location must satisfy **at least one** entry across all four lists, or it's a placement violation.

`must_reside_in_projects` resolves each configured project name to its assembly name via project discovery, then checks it identically to `must_reside_in_assemblies`. **There is no true type-to-`.csproj` mapping in this tool** — a project maps 1:1 to a single assembly name, so "must reside in project X" really means "must reside in the assembly that project X produces." For a typical one-project-per-assembly .NET solution this is exactly what you'd expect; it does not track physical file placement within a project. A project name that does not match any discovered project resolves to nothing and therefore never widens what's allowed (fail-closed, like other allow-only-style contracts in this tool).

### Naming expectations

`required_name_suffix`, `required_name_prefix`, `forbidden_name_suffix`, and `forbidden_name_prefix` are all optional and check the selected type's simple name.

### Declaring at least one expectation

A `type_placement` contract must declare at least one placement expectation (`must_reside_in_*`) or at least one naming expectation (`required_name_*` / `forbidden_name_*`). A selector with no expectation at all is rejected at policy load time as a configuration error — it can never produce a violation and is almost certainly a mistake.

### Violations

A selected type that fails placement, naming, or both produces a **single** violation for that type — a type failing both reports both the location and name mismatch on the same diagnostic, not two separate diagnostics. Diagnostics identify the type, the contract, the expected location and/or name, and the type's actual location and/or name.

`ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families.

## Scope: what's not covered here

- No regex or expression-language selectors.
- No runtime dependency-injection resolution.
- No semantic data-flow analysis.
- No automatic moving or renaming of source files — this contract family only reports violations.
- No method-body IL scanning — types are matched structurally (name, namespace, base type, interfaces, attributes), not by usage.
- `must_reside_in_projects` is assembly-name equivalence via project discovery, not physical `.csproj`-membership tracking (see above).
