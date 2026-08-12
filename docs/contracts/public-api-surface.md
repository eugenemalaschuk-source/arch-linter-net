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

### Example with a reviewed snapshot

For a real library the inline list is hundreds of signatures, so declare the surface in a reviewed snapshot file instead:

```yaml
contracts:
  strict_public_api_surface:
    - id: module-api
      name: module-api-declared
      assemblies: [Acme.Ledger.Module]
      api_snapshot: architecture/api/module-api.txt
      api_comparison: exact
      reason: The published module boundary is reviewed as a file diff, and removals are breaking.
```

```bash
arch-linter-net public-api capture --policy architecture/dependencies.arch.yml --contract module-api --output architecture/api/module-api.txt --ensure-built
```

### Example with an intentional-surface selector

For a modular assembly containing many CLR-`public` implementation/domain/configuration types that are not intended compatibility contracts, restrict the reviewed snapshot to the types you actually mean to support:

```yaml
contracts:
  strict_public_api_surface:
    - id: module-public-api
      name: module-public-api-declared
      assemblies: [Acme.Ledger.Module]
      surface_selector:
        has_attribute: Acme.Ledger.Module.Architecture.PublicApiContractAttribute
      api_snapshot: architecture/api/module-public.txt
      api_comparison: exact
      reason: Only the types marked as intentional compatibility surface are reviewed.
```

`PublicApiContractAttribute` is a user-owned marker — nothing ArchLinterNet ships — applied on the type alongside whatever attribute already establishes its architecture role:

```csharp
[ValueObject]          // however the repository establishes its primary architecture role
[PublicApiContract]    // user-owned orthogonal marker selecting API membership
public readonly record struct Money(decimal Amount, string Currency);
```

See [Selecting an intentional surface](#selecting-an-intentional-surface) below for the full selector vocabulary and the role-vs-membership distinction.

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

For an exported type, its own **directly declared** members (constructors, methods, properties, fields including `const`, and events) are in scope if they are `public`, `protected`, or `protected internal`. Compiler-generated members (property/event backing fields, `get_`/`set_`/`add_`/`remove_` accessor methods — represented instead by the property/event itself) are excluded, as is an enum's synthesized `value__` backing field (an enum's real exported surface is its literal members, e.g. `const MyApp.Color.Red: MyApp.Color`, not this CLR implementation detail). Members **inherited** from a base type are not re-reported against the derived type; they belong to the base type's own declared surface.

### Selecting an intentional surface

By default every exported type and member in `assemblies` is governed — this is unchanged and remains the behavior when no `surface_selector` is declared. `surface_selector` is an optional, bounded selector that restricts the governed surface to types the selector matches, letting a modular assembly's reviewed snapshot describe only its intentional compatibility contracts instead of every incidental `public` implementation/domain/configuration type.

**Semantic role is not the same thing as API membership.** ArchLinterNet's semantic classification model gives each type a single winning role (`ValueObject`, `Entity`, `Controller`, `Adapter`, etc.) plus metadata. `surface_selector` does **not** require, and must never be used to force, reclassifying a type to some `ApiContract`-style role merely to shrink a snapshot — a `ValueObject` selected into the reviewed API surface stays a `ValueObject` for every other semantic/contextual rule. Membership in the reviewed surface is an orthogonal, independent decision from the type's architectural role.

`surface_selector` accepts the same structural matcher fields `type_placement.types_matching` already supports, plus `role`:

| Field | Matches |
| --- | --- |
| `name_suffix` / `name_prefix` | Simple type name |
| `namespace` | Namespace, or a child namespace of it |
| `layer` | A namespace already resolved to a declared layer |
| `base_type` | Any type whose base-type chain includes the named type |
| `implements_interface` | Any type implementing the named interface |
| `has_attribute` | Any type carrying a custom attribute with the named full name |
| `role` | The type's existing single winning semantic role (read-only — selecting via `role` never assigns or changes it) |

At least one field must be populated — an empty `surface_selector` is rejected at policy load, the same invariant `types_matching` already enforces. Every populated field combines with **AND** semantics, exactly like `types_matching`.

`has_attribute` is the primary adoption path: a user-owned marker attribute (nothing ArchLinterNet ships) selects membership without touching the type's real architectural classification. `implements_interface`/`base_type` fit a repository that already has a stable contract abstraction; `namespace`/`layer`/name matching fit only when they express deliberate API intent, not a naming guess. `role` selects through the existing semantic role index and is meant for the case where a type's genuine primary classification already **is** `ApiContract` (or similar) — it is one more existing-evidence path, not the required mechanism for ordinary orthogonal membership.

The selected surface is used identically everywhere: strict/audit validation, `public-api capture`, `public-api diff`, `public-api update` (including `--dry-run`), `public-api migrate`, the CLI, and `ArchLinterNet.Testing` all resolve the same effective set of governed types — there is exactly one selected surface per contract, not a validation-only view.

Two failure modes are specific to selectors:

- A selector that matches **zero** exported types across the contract's resolved assemblies is a violation (`api_delta_kind: selector-zero-match`), reported at validation/capture time — the same "recorded, not thrown at load" treatment an unusable snapshot already gets, since resolving a selector needs the assemblies loaded. It applies to `public-api capture`/`diff`/`update`/`migrate` too, so a typo'd selector can never silently produce a near-empty snapshot.
- A selected member whose signature references another exported type declared in the **same contract's assemblies** (a first-party type) that the selector did **not** select fails closed rather than silently including the dependency. Ordinary BCL/external referenced types never trigger this — only types the contract itself governs. The diagnostic carries `unselected_first_party_dependency` naming the escaping type. Like the zero-match check, this also applies to `public-api capture`/`diff`/`update`/`migrate`, not only strict/audit validation — a selector configuration `validate` would reject can never produce a snapshot through the capture lifecycle that `validate` could then never pass against.

### Reviewed snapshots

`api_snapshot` points at a reviewed snapshot file. Its entries are unioned with `declared_api`, so a contract can use either source or both. The path is repository-local: it must be relative, must not be rooted, and must stay inside the policy boundary (the policy's directory, or its parent when the policy lives in an `architecture/` folder). The policy file itself is never a valid snapshot destination.

Two failure categories are deliberately different:

- An **absolute or boundary-escaping path** is a policy **load** error. No workflow can repair it.
- A **missing, unparsable, or foreign snapshot** is recorded and reported as a validation **violation**, not a load error. If loading failed here, a policy that already declares `api_snapshot: architecture/api/module-api.txt` could never run the `public-api capture` that creates that file for the first time. Validation still fails loudly, so a broken snapshot is never mistaken for "this contract declares nothing".

A snapshot belongs to exactly one contract: its `@contract` directive must match the contract's id, and it may only describe assemblies the contract declares. Attaching contract A's file to contract B, or a snapshot whose `@assembly` header names an undeclared assembly, is reported rather than silently accepted.

The file is a generated artifact with a deterministic, environment-free format — no timestamps, paths, machine names, or tool version stamps — so capturing the same surface twice is byte-identical on any host and a changed snapshot always means a changed surface.

A snapshot records the **exact grammar**: the legacy identity signature plus a bracketed detail suffix carrying what identity alone drops — the declaration's own exported visibility (non-`public` only; `public` stays implicit), constant and enum member values, enum underlying type, accessor shape and accessor visibility, `static`/`abstract`/`virtual`/`override`/`sealed`/`readonly`, `ref`/`out`/`in`/`params` parameter direction, and generic constraints. Without it, changing `public const int Version = 1` to `= 2`, narrowing `public void M()` to `protected void M()`, or `get;` to `get; set;`, would leave a byte-identical snapshot and pass exact mode. Inline `declared_api` entries keep using the legacy identity grammar unchanged.

```text
# arch-linter-net public API snapshot — generated by 'arch-linter-net public-api capture'.
# Review changes to this file like any other reviewed artifact; do not edit it by hand.
@format arch-linter-net/public-api-snapshot
@version 1
@contract module-api
@assembly Acme.Ledger.Module
class Acme.Ledger.Module.LedgerEntry [sealed]
const Acme.Ledger.Module.LedgerEntry.SchemaVersion: System.Int32 [value:3]
ctor Acme.Ledger.Module.LedgerEntry(System.Decimal)
enum Acme.Ledger.Module.EntryKind [underlying:System.Int32]
method Acme.Ledger.Module.LedgerEntry.TryParse(System.String, Acme.Ledger.Module.LedgerEntry&): System.Boolean [static, param1:out]
property Acme.Ledger.Module.LedgerEntry.Amount: System.Decimal [get, set:protected]
```

Assemblies and signatures are ordinal-sorted, duplicates are collapsed, lines are LF-terminated, and a file exceeding 200000 entries or 4000 characters per line is rejected. An unknown `@` directive or an unsupported `@version` is rejected rather than ignored, so a file written by a newer build fails loudly instead of losing entries.

Manage snapshots with the [`public-api` command](../cli/index.md#public-api):

```bash
arch-linter-net public-api diff --contract module-api --snapshot architecture/api/module-api.txt
arch-linter-net public-api update --contract module-api --snapshot architecture/api/module-api.txt --dry-run
```

### Comparison mode

`api_comparison` selects what counts as a violation:

- `additions_only` (default, and the historical behavior) — only exported surface that is not declared.
- `exact` — additionally reports declared signatures the assembly no longer exports (`removed`) and declared members whose normalized signature changed (`changed`).

Exact mode correlates the two sides by **assembly plus an identity key** — declaration kind, fully qualified name including generic arity, and parameter count — so re-typing a parameter or a return type reports **one** `changed` violation carrying both the previous and the current signature, rather than an unrelated removal plus addition. Adding an overload with a different parameter count stays an addition. Because assembly is part of the key, two assemblies exporting the same fully qualified signature stay distinct: removing it from one is never masked by the copy in the other, and the two are never paired into a cross-assembly `changed` record. This covers enum members and public constants, which reflect as `const` fields carrying their value.

Exact mode only reports removals and changes when every assembly the contract names actually resolved: against a partially resolved contract, every member of the missing assembly would masquerade as a removal.

Removals and changes are reported for the union of the contract's declared surface. Inline `declared_api` entries carry no assembly attribution, so they act as **wildcards**: they match a signature in any of the contract's assemblies, and a removal that came from an inline list is attributed to the assembly that still exports the same member identity, or reported without an assembly name when nothing does.

### Undeclared surface

Any exported type or member whose normalized signature is not present in the declared surface (`declared_api` plus any `api_snapshot` entries) is a violation.

### Public constants

By default (`forbid_public_constants_unless_declared: false`), an exported `const` field is treated exactly like any other member — undeclared is a violation, declared passes.

Setting `forbid_public_constants_unless_declared: true` adds a stricter, independent check: an exported `const` field is a violation **unless its fully-qualified member name** (e.g. `MyApp.Foo.Version`, no signature/type suffix) **is listed in `allowed_public_constants`** — even if its full signature is already present in `declared_api`. This matters because public constants are inlined by consumers at compile time, so acknowledging them in the general API-surface list is not the same as deliberately deciding to keep exposing one as a `const` (versus, say, a `static readonly` field).

### Violations

Each violation identifies the contract, the declaring assembly, the declaring type, the normalized signature of the undeclared member or forbidden constant, the member's visibility (`public`, `protected`, or `protected internal`), and whether the violation reason is an undeclared exported member, a removed member, a changed signature, or a forbidden public constant.

Surface deltas carry a normalized delta record — `api_delta_kind` (`added`, `removed`, `changed`, `snapshot-unusable` for a missing/unparsable/foreign snapshot, or `selector-zero-match` for a `surface_selector` that matched nothing) and, for a change, `previous_api_signature`. A selected member depending on an unselected first-party type instead carries `unselected_first_party_dependency`, naming the escaping type. Human output renders them inline (`delta: changed, previous_signature: ...` / `reason: unselected_first_party_dependency, unselected_dependency: ...`), the JSON CI artifact exposes the same fields, and SARIF carries the same keys in each result's `properties`, so all formats describe the same records. When a `const` field is simultaneously undeclared **and** fails the `forbid_public_constants_unless_declared` check, it is reported once, as a forbidden public constant (the stricter of the two reasons). `ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families. For every other public-api-surface violation reason, `forbidden_reference` is the normalized signature string; for an unselected first-party dependency, it is the escaping type's fully qualified name instead (the signature that depends on it is still available separately, as `undeclared_api_signature`/`UndeclaredApiSignature`) — the same source/target distinction `type_placement`-style families already use for their own `ignored_violations` entries.

## Scope: what's not covered here

- Removed and changed signatures are detected only under `api_comparison: exact`, and only as normalized-signature drift. This is still not a substitute for full binary/package compatibility validation.
- No automatic semantic-version decision: the tool reports what changed, not whether the change is major, minor, or patch.
- No automatic approval or commit of an updated snapshot — `public-api update` writes the file, a human still reviews the diff.
- No runtime dependency-injection resolution or semantic data-flow analysis.
- No automatic API-review approval or code-ownership enforcement.
- No automatic rewriting of source visibility.
- Reflection-based (like `protected` and `type_placement`), not project-aware Roslyn compilation.
- `surface_selector` is not a new annotation/tag/classification engine and does not ship a built-in marker attribute — it reuses the existing `type_placement` matcher vocabulary and semantic role index, and a selected type keeps its single existing winning role.
- The first-party-dependency check is one hop (a selected member's own signature), not a general recursive DTO/domain/persistence/framework/version exposure graph.
