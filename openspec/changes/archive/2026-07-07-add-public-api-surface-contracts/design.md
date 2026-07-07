## Context

Every existing contract family checks *references* (source layer/type A references forbidden namespace/group/assembly B) or, in the most recent precedent (`type_placement`, #85), *where a type lives and how it's named*. Type metadata across all contracts today is reflection-based (`System.Type` via `ArchitectureTypeIndex`/`ArchitectureTypeScanner`), not Roslyn-symbol-based; project-aware Roslyn (#61) stays scoped to method-body call resolution and exposes no general type-metadata query API.

Issue #83 asks for something new again: a contract that inspects the *exported surface* of one or more assemblies — every `public`/`protected`/`protected internal` type and member reachable from outside the assembly — and compares it against a declared allowlist, independent of any layer/namespace concept. This is closer in spirit to Roslyn's `PublicAPIAnalyzer` (`PublicAPI.Shipped.txt`), but the user has already decided (manual-review item from the issue) that v1 uses an ArchLinterNet-native inline YAML declaration instead of that two-file text format, to stay inside the existing YAML policy pipeline with one parser, one schema, and one deterministic diff-friendly place to look.

## Goals / Non-Goals

**Goals:**
- One contract family, `public_api_surface`, that targets assemblies by name, enumerates every exported type and member via reflection, normalizes each into a deterministic signature string, and reports any exported signature not present in the contract's `declared_api` list.
- Treat `public`, `protected`, and `protected internal` uniformly as "exported" by default — both are visible outside the declaring assembly to any external subclass.
- Give policy authors an opt-in, explicit lever (`forbid_public_constants_unless_declared` + `allowed_public_constants`) against the specific risk of public constants (compile-time inlining breaks binary compatibility on library updates) without changing default behavior for existing/simple policies.
- Reuse the existing reflection-based type index and the `ArchitectureViolation`/diagnostic-mapper pattern exactly as `type_placement` did — zero changes to `ArchitectureContractExecutor`.
- Deterministic output: one violation per undeclared exported signature, ordinally sorted enumeration.

**Non-Goals:**
- Binary/package compatibility validation (e.g. detecting a breaking signature *change* to an already-declared member) — this contract only detects *undeclared exported surface*, not surface drift/removal. (Issue explicit non-goal: "Replacing .NET binary compatibility validation.")
- The `PublicAPI.Shipped.txt`/`Unshipped.txt` two-file, shipped/unshipped-promotion workflow — explicitly rejected by the user in favor of inline YAML.
- Runtime DI resolution or semantic data-flow analysis (issue explicit non-goals).
- Automatic API-review approval, code-ownership enforcement, or automatic visibility rewriting (issue explicit non-goals).
- Project-aware Roslyn/Buildalyzer compilation — this is reflection-based metadata inspection only, consistent with `protected-surface`/`type-placement`.

## Decisions

**Single family, `public_api_surface`, covering both the general undeclared-surface check and the constants lever.**
The issue frames declaration and constant-forbidding as one contract (`forbid_public_constants_unless_declared` is a field alongside `assemblies`/`declaration_file` in the issue's own example YAML). Splitting into two families would duplicate the assembly-targeting/declaration-list schema for no behavioral benefit.

**Declaration format: inline `declared_api: [...]` string list, own normalized signature grammar.**
Format: `"<kind> <FullyQualifiedName>[(<param types>)][: <member type>]"`, where `kind` ∈ `class`/`interface`/`struct`/`enum`/`delegate`/`const`/`field`/`property`/`event`/`method`/`ctor` (records reflect as an ordinary `class`/`struct` — reflection has no reliable, non-heuristic way to distinguish a record from a hand-written type, and the issue's fixture list doesn't require it). Examples:
- `class ArchLinterNet.Core.Foo`
- `const ArchLinterNet.Core.Foo.Version: string`
- `field ArchLinterNet.Core.Foo.Count: int`
- `property ArchLinterNet.Core.Foo.Name: string`
- `event ArchLinterNet.Core.Foo.Changed: System.EventHandler`
- `method ArchLinterNet.Core.Foo.Bar(int, string): void`
- `ctor ArchLinterNet.Core.Foo(int)`
- Generic type: `` class ArchLinterNet.Core.Box`1 `` (arity comes straight from `Type.FullName`); generic method: `` method ArchLinterNet.Core.Foo.Map`1(!0): !!0 `` (type parameters normalized positionally — `!N` for a declaring-type parameter, `!!N` for a declaring-method parameter, matching CLR ilasm convention — so renaming a generic parameter alone doesn't churn the declaration).
Parameter/member types are rendered via a small recursive helper over `Type.FullName`/`GetGenericArguments()`/`GetElementType()` (arrays, by-ref, pointers, and closed generic instantiations all resolved structurally, since `Type.FullName` alone is `null` for any open generic instantiation such as `List<T>`), keeping the grammar simple string concatenation — no attempt at C#-idiomatic pretty-printing (e.g. no `IReadOnlyList<string>` sugar; nested generics render as CLR-style `` System.Collections.Generic.IReadOnlyList`1[System.String] ``). This trades readability for zero ambiguity and avoids writing a new C#-syntax printer.
- Alternative considered: reuse XML-doc-comment ID syntax (`M:`, `P:`, `F:`, `T:` prefixes as Roslyn/Sandcastle use). Rejected — that grammar is optimized for tooling round-tripping, not for a human policy author hand-writing or reading a diff; the issue asks for something "deterministic, readable, and schema-backed," and a plain `kind Name(params): type` reads closer to C# than doc-comment IDs do.
- Alternative considered: full C#-syntax pretty-printing (resolving generic argument names, using-alias-aware short type names). Rejected — requires a real C# syntax printer (essentially reinventing part of Roslyn's `SymbolDisplayFormat`) for a reflection-based (`System.Type`), not symbol-based, pipeline; `FullName`-based rendering is directly available on every `Type`/`MemberInfo` already in scope and is fully deterministic without new dependencies.

**Exported = type is `public`, or `protected`/`protected internal` nested inside an already-exported enclosing type chain.**
A `protected` nested type inside an `internal` outer type is not actually reachable from outside the assembly (nothing outside can hold a reference to the outer type to see the nested one), so it is not part of the exported surface. This walks `Type.DeclaringType` up the chain checking `IsPublic`/`IsNestedPublic`/`IsNestedFamily`/`IsNestedFamORAssem` at each level, mirroring how `IsVisible`-style checks are normally reasoned about, but implemented as an explicit walk (not `Type.IsVisible`, which for nested types requires the *whole* chain to be `public` specifically, and does not treat `protected`-visible-through-inheritance as "visible" the way this contract intentionally does).

**Members captured: constructors, methods (excluding property/event accessor methods), properties, fields (including `const`), events — `DeclaredOnly`, not inherited.**
`BindingFlags.DeclaredOnly | Public | NonPublic | Instance | Static`, then filtered to `public`/`family` (protected)/`famorassem` (protected internal) visibility, minus compiler-generated members (`[CompilerGenerated]`, property/event backing methods identified via `IsSpecialName` + `get_`/`set_`/`add_`/`remove_` prefix, already covered by the property/event entry itself). Only members *declared on the type itself* are in scope — an inherited public method from a base class is that base type's declaration surface, not a re-declaration by the derived type (matches the issue's "detect accidental exported types/members," which is about a type's own declared surface, not what it inherits).
- Alternative considered: include inherited members too (full effective surface per type). Rejected — every base type in the target assembly is itself walked and reported independently, so its members are already covered once at their declaring type; re-reporting them against every derived type would produce massive duplicate violations and make `declared_api` unmanageable (one base method would need re-declaring against every subclass).

**Constants lever: `forbid_public_constants_unless_declared` (bool, default `false`) + `allowed_public_constants` (list of fully-qualified member names, not full signatures).**
When `false` (default), a `const` field member is treated exactly like any other member — undeclared is a violation, declared (present in `declared_api`) passes. When `true`, an *additional* check runs: any exported `const` field whose fully-qualified name (`DeclaringType.FullName + "." + Name`, no signature/type suffix) is not present in `allowed_public_constants` produces a distinct violation, **even if the const's full signature is present in `declared_api`** — i.e. turning the flag on means "the general declaration mechanism is not sufficient acknowledgment for a const; it needs the narrower, purpose-specific `allowed_public_constants` opt-in too." This gives the flag real teeth (it is not a no-op when the const is already declared) while keeping the default (`false`) fully backward compatible with the general declaration-only model.
- Alternative considered: make `forbid_public_constants_unless_declared` purely cosmetic (same failure as the generic undeclared check, just a clearer message). Rejected — would make the flag a no-op for any const author already diligently maintaining `declared_api`, which contradicts the issue's explicit ask for a lever "to forbid public constants unless explicitly declared **or explicitly allowed by rule**" (two distinct escape hatches implies two distinct checks, not one).

**Diagnostics: extend `ArchitectureViolation` with two optional fields; add a dedicated `PublicApiSurfaceDiagnostic`.**
`UndeclaredApiSignature` (the normalized signature string that triggered the violation) and `ForbiddenPublicConstant` (bool?, set `true` only for the constants-lever violation described above). `SourceType` carries the declaring type's full name. `ForbiddenNamespace`/`ForbiddenReferences` (required fields on the shared record) are populated with fallback values (`ForbiddenNamespace = "public API surface"`, `ForbiddenReferences = [UndeclaredApiSignature]`) so any generic/legacy formatting path still renders something sane. `ArchitectureDiagnosticMapper.FromViolation` gets a new branch, checked alongside the existing `ExpectedTypeLocation`/`ForbiddenExternalGroup`/`ForbiddenPackageGroup` branches, dispatching to `PublicApiSurfaceDiagnostic` when `UndeclaredApiSignature` is set. `ArchitectureDiagnosticFormatter` gets human-text and CI-JSON rendering.
- Alternative considered: a fully separate violation/diagnostic pipeline. Rejected for the same reason as `type_placement`: `ArchitectureContractExecutor`/`ArchitectureHandlerResult` are hard-coded to `List<ArchitectureViolation>`; a parallel shape would break the "zero executor changes per family" invariant every prior contract preserves.

**Load-time validation: reject a `public_api_surface` contract with no `assemblies`.**
Mirrors existing conventions (e.g. `type_placement` rejecting a selector with no expectation) — `ArchitecturePolicyDocumentLoader` gains a check that `assemblies` is non-empty; a contract with nothing to scan is a configuration error, not a silent no-op.

## Risks / Trade-offs

- **[Risk] `FullName`-based signature rendering is not C#-idiomatic (CLR generic syntax, assembly-qualified nested generics) and may be unfamiliar to read/hand-author.** → Documented explicitly in `docs/contracts/public-api-surface.md` with worked examples; mitigated by the fact that in practice most policy authors generate the initial `declared_api` list from a real assembly (future baseline/explain tooling) rather than hand-typing every signature, matching how `PublicAPI.Shipped.txt` is normally maintained too (generated, then diffed).
- **[Risk] No detection of *removed* or *changed* declared signatures (only additions).** → Explicitly out of scope per the issue's non-goals (binary compatibility validation is a different, larger capability); this contract only prevents accidental *new* exported surface, which is the issue's stated goal.
- **[Risk] Reflection-based nested-type visibility walk could diverge from `Type.IsVisible`/Roslyn's own "effectively public" semantics in edge cases (e.g. deeply nested generic types).** → Consistent with every other reflection-based contract already in this codebase; defensive handling matches `ArchitectureTypeScanner.GetLoadableTypes`'s existing tolerance for reflection failures (skip, don't throw).
- **[Risk] `forbid_public_constants_unless_declared`'s "declared-but-still-forbidden" behavior is a subtler rule than the rest of the contract family and could surprise a policy author.** → Mitigated by documenting it explicitly with a worked example in `docs/contracts/public-api-surface.md`, and by the flag defaulting to `false` so it never applies unless explicitly opted into.

## Migration Plan

Purely additive: new YAML contract-group keys, new schema defs, new docs page, two new optional `ArchitectureViolation` fields (existing consumers unaffected), one new `ArchitectureDiagnosticKind` enum value (additive). No existing contract family, schema field, or CLI behavior changes. No migration or rollback beyond normal code review/revert.
