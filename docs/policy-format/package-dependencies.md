# Package Dependencies

Use `packages` to declare named groups of NuGet package IDs, so policies can forbid or allow-list *declared package references* (`PackageReference` items) independently of whether any first-party type actually touches a type from that package.

Typical examples:

- Entity Framework Core / ORM packages a domain project must not reference;
- Unity runtime/editor NuGet packages;
- ASP.NET hosting packages;
- cloud/infrastructure SDK packages;
- test-framework packages that only test projects may reference.

## YAML shape

```yaml
packages:
  forbidden_infra:
    package_ids:
      - Newtonsoft.Json
    package_prefixes:
      - Microsoft.EntityFrameworkCore
      - AWSSDK

  test_frameworks:
    package_ids:
      - NUnit
      - xunit
    package_prefixes: []
```

## Matching rules

`package_ids` match a `PackageReference`'s package ID exactly, case-insensitively (NuGet package IDs are case-insensitive).

`package_prefixes` match a package ID that equals the prefix or is a dot-segment child of it (e.g. `Microsoft.EntityFrameworkCore` matches `Microsoft.EntityFrameworkCore.SqlServer` but not `Microsoft.EntityFrameworkCoreTools`), case-insensitively.

Package matching is purely static: it reads `PackageReference` items from each project's `.csproj` and resolves missing versions from the nearest ancestor `Directory.Packages.props` (central package management). It does not perform NuGet restore or resolve the transitive package graph.

## Use with contracts

```yaml
contracts:
  strict_package_dependency:
    - id: domain-no-ef-core
      name: domain-must-not-reference-ef-core
      source: MyApp.Domain
      forbidden: [forbidden_infra]
      reason: Domain code must not declare infrastructure SDK package references.
```

Use audit while discovering existing package leakage:

```yaml
contracts:
  audit_package_dependency:
    - id: audit-application-sdk-packages
      name: audit-application-sdk-packages
      source: MyApp.Application
      forbidden: [forbidden_infra]
      reason: Discover SDK package leakage before making this strict.
```

Restrict a project to only an allow-listed set of package groups with `package_allow_only`:

```yaml
contracts:
  strict_package_allow_only:
    - id: domain-allowed-packages
      name: domain-may-only-reference-test-frameworks
      source: MyApp.Domain.Tests
      allowed: [test_frameworks]
      reason: Domain tests may only reference declared test-framework packages.
```

## Package dependencies vs external dependencies

Prefer `packages`/`package_dependency` contracts when you want to forbid a **declared package reference** regardless of whether any compiled type currently uses it — this catches architecture drift the moment a forbidden package is added to a `.csproj`, before any code references it.

Prefer `external_dependencies`/`strict_external` contracts when you want to detect **observed type references** to vendor/framework types in compiled code, including references buried in method bodies.

The two families are independent: a policy can declare both, and each is evaluated and reported without affecting the other.
