# Package Dependency Contracts

Package dependency contracts enforce rules over *declared* NuGet package references (`PackageReference` items in `.csproj`, resolved through central package management when a version is omitted) — independent of whether any compiled type actually references a type from that package.

Two families are covered on this page:

- **Package dependency** (`strict_package_dependency`/`audit_package_dependency`) — a source project/assembly must not declare a `PackageReference` matching any package group in a `forbidden` list.
- **Package allow-only** (`strict_package_allow_only`/`audit_package_allow_only`) — a source project/assembly may only declare `PackageReference`s matching package groups in an `allowed` list; any other declared package reference is a violation.

Package groups (`package_ids`/`package_prefixes`) are declared in a top-level `packages` section — see [package dependencies policy format](../policy-format/package-dependencies.md) for the YAML shape and matching rules.

## Package dependency example

```yaml
packages:
  forbidden_infra:
    package_prefixes: [Microsoft.EntityFrameworkCore]

contracts:
  strict_package_dependency:
    - id: domain-no-ef-core
      name: domain-must-not-reference-ef-core
      source: MyApp.Domain
      forbidden: [forbidden_infra]
      reason: Domain must stay free of infrastructure package dependencies.
```

## Package allow-only example

```yaml
packages:
  test_frameworks:
    package_ids: [NUnit, xunit]

contracts:
  strict_package_allow_only:
    - id: domain-tests-allowed-packages
      name: domain-tests-may-only-reference-test-frameworks
      source: MyApp.Domain.Tests
      allowed: [test_frameworks]
      reason: Test projects may only reference declared test-framework packages.
```

The `source` of every package dependency/allow-only contract must be listed in `analysis.target_assemblies`; a name that isn't a declared target assembly fails policy loading with an actionable error instead of silently being skipped.

## Semantics

Both families detect **direct, statically declared package references only** — they parse each project's `.csproj` `PackageReference` items (and resolve a missing version from the nearest ancestor `Directory.Packages.props`), and do not perform NuGet restore or resolve the transitive package graph.

Both families accept an optional `dependency_depth` field (`direct` by default). **`direct` is the only supported value.** Declaring `dependency_depth: transitive` fails policy loading with an actionable error rather than being silently ignored — package dependency contracts have no notion of a resolved package graph to walk transitively.

**Package dependency**: for each package group in `forbidden`, a violation is reported per matched `PackageReference`, aggregated into one violation per forbidden group naming every matched package (with resolved version, when known).

**Package allow-only**: a violation is reported once per source, listing every declared `PackageReference` that does not match any package group in `allowed`, sorted by package ID with duplicates removed.

Violations identify the source project/assembly, the forbidden package group (for `package_dependency`), and each matched package reference as `PackageId@Version` (or bare `PackageId` when no version could be resolved).

`ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families; here `source_type` holds the source assembly name and `forbidden_reference` holds the package ID.

## Package dependency contracts vs external dependency contracts vs assembly dependency contracts

These checks operate at different boundaries and are independent of one another:

- **Package dependency/allow-only contracts** (this page) check *declared NuGet package references* — useful for catching a forbidden dependency the moment it's added to a `.csproj`, before any code references it.
- **[External dependency contracts](external-dependencies.md)** (`strict_external`/`audit_external`) check *observed type references* to vendor/framework types in compiled code, including method-body IL scanning.
- **[Assembly dependency contracts](assembly-dependency.md)** (`strict_assembly_dependency`/`audit_assembly_dependency`) check *compiled assembly-to-assembly* references, not package declarations.

## Scope: what's not covered here

NuGet restore, lock-file resolution, or the transitive package dependency graph are not evaluated — only what is statically declared in `.csproj`/`Directory.Packages.props`. Vulnerability and license compliance scanning are also out of scope for this contract family.
