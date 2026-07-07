# Public API Surface Contracts

Public API surface contracts declare the intended exported API surface of one or more assemblies — every `public`, `protected`, or `protected internal` type and member — and report any exported type or member that isn't declared. This governs the *published boundary* of a library, catching accidental exports before they reach a NuGet package, not general dependency direction between layers.

Groups:

- `strict_public_api_surface`
- `audit_public_api_surface`

## Example

```yaml
contracts:
  strict_public_api_surface:
    - id: core-public-api
      name: core-public-api-declared
      assemblies: [ArchLinterNet.Core]
      declared_api:
        - "class ArchLinterNet.Core.Foo"
        - "ctor ArchLinterNet.Core.Foo()"
        - "method ArchLinterNet.Core.Foo.Bar(System.Int32): System.Void"
        - "property ArchLinterNet.Core.Foo.Name: System.String"
        - "const ArchLinterNet.Core.Foo.Version: System.String"
      forbid_public_constants_unless_declared: true
      allowed_public_constants:
        - "ArchLinterNet.Core.Foo.Version"
      reason: Track the exact exported surface of Core before every NuGet release.
```

## When to use

Use public API surface contracts for library assemblies you ship to consumers (NuGet packages, shared internal libraries), where an accidental `public` type or member is a silent breaking-change/compatibility risk:

- catch a type or member left `public` by mistake (missing `internal`);
- make public constants visible in code review before they're inlined into consumer binaries;
- give CI a deterministic gate on API surface growth ahead of a release.

This is not a substitute for `.NET` binary/package compatibility validation (detecting a breaking *change* to an already-declared member) — it only detects **undeclared** exported surface, additions that were never acknowledged.

## Semantics

### Declaring the exported surface

`assemblies` names one or more target assemblies; each name must be declared in `analysis.target_assemblies`, and a contract with an empty or missing `assemblies` list is rejected at policy load time. Both are policy load-time errors, not silent no-ops — an assembly name that doesn't resolve is never treated as "nothing to check for it."

`declared_api` is a list of normalized signature strings. The grammar is `"<kind> <FullyQualifiedName>[(<param types>)][: <member type>]"`, where `kind` is one of `class`, `interface`, `struct`, `enum`, `delegate`, `const`, `field`, `property`, `event`, `method`, or `ctor` (records reflect as an ordinary `class`/`struct`; reflection cannot reliably distinguish a record from a hand-written type). Examples:

- `class MyApp.Foo`
- `ctor MyApp.Foo(System.Int32)`
- `method MyApp.Foo.Bar(System.Int32, System.String): System.Void`
- `property MyApp.Foo.Name: System.String`
- `field MyApp.Foo.Count: System.Int32`
- `const MyApp.Foo.Version: System.String`
- `event MyApp.Foo.Changed: System.EventHandler`
- Nested type: `class MyApp.Outer+Inner` (CLR nested-type notation, `+` not `.`).
- Generic type: `` class MyApp.Box`1 `` (arity comes from the CLR, same as `Type.FullName`).
- Generic method: `` method MyApp.Foo.Map`1(!0): !!0 `` — generic parameters are rendered **positionally**, not by their source name: `!N` is the *N*th type parameter of the declaring type, `!!N` is the *N*th type parameter of the declaring method. This means renaming a generic parameter alone never changes the declared signature.
- Array rank is preserved: `int[]` renders as `System.Int32[]`, `int[,]` as `System.Int32[,]`, `int[,,]` as `System.Int32[,,]` — each rank is a distinct signature.

Parameter, field, property, and return types are rendered via their CLR full name (e.g. `System.Int32`, not `int`) — this is a deterministic, own grammar, not an attempt at C#-idiomatic pretty-printing.

### What counts as exported

A type is exported if it is `public`, or if it is `protected`/`protected internal` **and** every enclosing type in its nesting chain is itself exported. A `protected` nested type inside an `internal` outer type is unreachable from outside the assembly, so it is out of scope even though the modifier says "protected."

For an exported type, its own **directly declared** members (constructors, methods, properties, fields including `const`, and events) are in scope if they are `public`, `protected`, or `protected internal`. Compiler-generated members (property/event backing fields, `get_`/`set_`/`add_`/`remove_` accessor methods — represented instead by the property/event itself) are excluded. Members **inherited** from a base type are not re-reported against the derived type; they belong to the base type's own declared surface.

### Undeclared surface

Any exported type or member whose normalized signature is not present in `declared_api` is a violation.

### Public constants

By default (`forbid_public_constants_unless_declared: false`), an exported `const` field is treated exactly like any other member — undeclared is a violation, declared passes.

Setting `forbid_public_constants_unless_declared: true` adds a stricter, independent check: an exported `const` field is a violation **unless its fully-qualified member name** (e.g. `MyApp.Foo.Version`, no signature/type suffix) **is listed in `allowed_public_constants`** — even if its full signature is already present in `declared_api`. This matters because public constants are inlined by consumers at compile time, so acknowledging them in the general API-surface list is not the same as deliberately deciding to keep exposing one as a `const` (versus, say, a `static readonly` field).

### Violations

Each violation identifies the contract, the declaring type, and the normalized signature of the undeclared member or forbidden constant. `ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, where `forbidden_reference` is the normalized signature string.

## Scope: what's not covered here

- No detection of *removed* or *changed* declared signatures — only additions. This is not a substitute for full binary/package compatibility validation.
- No runtime dependency-injection resolution or semantic data-flow analysis.
- No automatic API-review approval or code-ownership enforcement.
- No automatic rewriting of source visibility.
- Reflection-based (like `protected` and `type_placement`), not project-aware Roslyn compilation.
