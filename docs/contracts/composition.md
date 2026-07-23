# Composition Contracts

Composition contracts restrict where composition-root and service-locator APIs may be called from: dependency-injection registration (`IServiceCollection.AddSingleton`, etc.), service-locator resolution (`IServiceProvider.GetService`), and container `Resolve`/`Register` calls in Unity/VContainer-style bootstraps. This is a static reflection/IL-based check on call sites outside a declared composition boundary, not runtime dependency-injection resolution.

Groups:

- `strict_composition`
- `audit_composition`

## Example

```yaml
contracts:
  strict_composition:
    - id: service-locator-confined-to-composition-root
      name: service-locator-confined-to-composition-root
      allowed_only_in_layers: [composition]
      forbidden_apis:
        - System.IServiceProvider.GetService
        - Microsoft.Extensions.DependencyInjection.IServiceCollection.
      reason: Service resolution and DI registration must happen only in the composition root.
```

A Unity/VContainer-style bootstrap boundary looks the same shape, with container-specific member names or namespace prefixes:

```yaml
contracts:
  strict_composition:
    - id: container-confined-to-bootstrap
      name: container-confined-to-bootstrap
      allowed_only_in_namespaces: [MyGame.Bootstrap]
      forbidden_apis:
        - Resolve
        - Register
      reason: Container resolution/registration must happen only during bootstrap.
```

## When to use

Use composition contracts when a service-locator or DI-registration API should be confined to a composition root or bootstrap boundary:

- `IServiceProvider.GetService`/`IServiceCollection.AddSingleton`-style calls should occur only in a server application's composition root, not scattered through application/domain code;
- Unity/VContainer-style container `Resolve`/`Register` calls should occur only during a scene or lifetime-scope bootstrap, not from arbitrary gameplay code.

For forbidding calls scoped to a single named source layer (rather than an allow-listed boundary spanning the rest of the codebase), use [method-body contracts](method-body.md) instead — composition contracts invert that shape: they scan every type *outside* the allow-list.

## Semantics

### Composition boundary

`allowed_only_in_layers`, `allowed_only_in_namespaces`, `allowed_only_in_projects`, `allowed_only_in_assemblies`, and `allowed_only_in_types` together form an allow-list: any type whose location does not satisfy **at least one** entry across all five lists is scanned for forbidden API calls. A type inside the boundary is never scanned — calling a forbidden API from inside the composition boundary is exactly what the boundary is for.

A contract must declare at least one entry across these five lists; a contract with none is rejected at policy load time (every call site in the codebase would otherwise be considered outside the boundary).

`allowed_only_in_projects` resolves each configured project name to its assembly name via project discovery — the same assembly-name-equivalence semantics documented for `type_placement`'s `must_reside_in_projects`.

`allowed_only_in_types` is a direct assembly + type identity selector, narrower than `allowed_only_in_assemblies` (which allows every type in the named assembly) or `allowed_only_in_namespaces` (every type in the namespace). Each entry requires both `assembly` and `type` (the type's fully-qualified name) and is matched by exact string equality — no globbing, no semantic-role classification or attribute-based matching. Use it when a single global/top-level type, such as one host's `Program`, must be the composition boundary without also allowing the rest of its assembly or namespace:

```yaml
contracts:
  strict_composition:
    - id: api-host-composition-root
      name: api-host-composition-root
      allowed_only_in_types:
        - assembly: Product.Api
          type: Product.Api.Program
      forbidden_apis:
        - System.IServiceProvider.GetService
      reason: Only Product.Api.Program may resolve services directly; the rest of Product.Api must go through DI.
```

In a multi-host solution with `Product.Api.Program`, `Product.Admin.Program`, and `Product.Web.Program`, each host gets its own `allowed_only_in_types` entry (or its own contract) — a sibling type in the same assembly or namespace that isn't named by an entry is still scanned, and a same-named `Program` type in a different assembly is a distinct entry entirely (see Violations below).

### Matching surface

`forbidden_apis` uses the same call-pattern vocabulary as [method-body contracts](method-body.md):

- member names;
- `Type.Member` names;
- fully qualified members;
- namespace or type prefixes (entries ending in `.`).

A contract must declare at least one `forbidden_apis` entry; a contract with none is rejected at policy load time.

Scanning is reflection/IL-only (no Roslyn dual-scan): every loaded type's methods and constructors are inspected via `MethodBase.GetMethodBody()`, matching the approach used by the method-body IL fallback. This means calls made through delegates, expression trees, or reflection-invoked members are not visible to the scanner.

### Violations

Each violation identifies the calling type and source member outside the composition boundary, the matched forbidden API's fully-qualified name, and the expected composition boundary description. Violations are emitted deterministically: types ordered by fully-qualified name, matched APIs within a type ordered ordinally, source members ordered ordinally within each matched API, with at most one violation per (type, source member, matched API) tuple.

`ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, matching the calling type and the matched forbidden API's fully-qualified name.

Violation/baseline identity is assembly- and member-qualified: two same-named types in different assemblies (for example two `Program` types in two host assemblies) are never conflated, and two distinct forbidden-call occurrences — whether from different source members of the same type or from two separate call sites to the same forbidden API within the same source member — get distinct identities. Baselining one occurrence never suppresses an unrelated same-named or same-member occurrence elsewhere. The violating type's declaring assembly is also included as `source_assembly` in human, `--json`, `--explain`, **and SARIF** output (`FormatCompositionContextForHumans`/`ApplyCompositionCiFields`/`ArchitectureSarifFormatter.BuildCompositionProperties`), so two same-named types in different assemblies are visibly distinguishable in every output format, not just at baseline-matching time.

## Non-goals

- **Runtime DI resolution correctness is not validated.** The contract detects static call sites to selected APIs outside a declared boundary; it does not resolve, simulate, or verify runtime service registration or resolution, and it does not prove every service is registered correctly.
- Not a substitute for reflection/plugin-loading validation.
- No semantic data-flow analysis — only static reflection/IL member-reference matching.
