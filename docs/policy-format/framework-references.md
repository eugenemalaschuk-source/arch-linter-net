# Framework References

Use `framework_references` to declare named groups of MSBuild `FrameworkReference` names, so policies can forbid or allow-list *declared framework references* (`FrameworkReference` items) independently of whether any first-party type actually touches a type from that shared framework.

Typical examples:

- ASP.NET Core's shared framework a domain/module project must not reference;
- Windows Desktop (WPF/WinForms) shared framework references outside UI projects;
- restricting a worker/library project to only the core shared framework.

## YAML shape

```yaml
framework_references:
  forbidden_web:
    framework_names:
      - Microsoft.AspNetCore.App
    framework_name_prefixes: []

  core_only:
    framework_names:
      - Microsoft.NETCore.App
```

## Matching rules

`framework_names` match a `FrameworkReference`'s `Include` name exactly, case-insensitively.

`framework_name_prefixes` match a framework name that equals the prefix or is a dot-segment child of it (e.g. `Microsoft.AspNetCore` matches `Microsoft.AspNetCore.App` but not `Microsoft.AspNetCoreTools`), case-insensitively.

Framework matching is purely static: it reads `FrameworkReference` items' `Include` and `Condition` attributes from each project's `.csproj`. `FrameworkReference` has no `Version` attribute, so there is no version resolution.

## Use with contracts

```yaml
contracts:
  strict_framework_dependency:
    - id: domain-no-aspnetcore
      name: domain-must-not-reference-aspnetcore
      source: MyApp.Domain
      forbidden: [forbidden_web]
      reason: Domain code must not declare the ASP.NET Core shared framework.
```

Use audit while discovering existing framework leakage:

```yaml
contracts:
  audit_framework_dependency:
    - id: audit-application-web-framework
      name: audit-application-web-framework
      source: MyApp.Application
      forbidden: [forbidden_web]
      reason: Discover shared-framework leakage before making this strict.
```

Restrict a project to only an allow-listed set of framework groups with `framework_allow_only`:

```yaml
contracts:
  strict_framework_allow_only:
    - id: worker-allowed-frameworks
      name: worker-may-only-reference-core
      source: MyApp.Worker
      allowed: [core_only]
      reason: Worker projects may only reference the core shared framework.
```

## Framework references vs package dependencies vs external dependencies

Prefer `framework_references`/`framework_dependency` contracts when you want to forbid a **declared `FrameworkReference`** regardless of whether any compiled type currently uses it.

Prefer `packages`/`package_dependency` contracts for **declared `PackageReference`** items — a separate MSBuild item type with its own version/central-package-management semantics.

Prefer `external_dependencies`/`strict_external` contracts when you want to detect **observed type references** to vendor/framework types in compiled code, including references buried in method bodies.

These three families are independent: a policy can declare all of them, and each is evaluated and reported without affecting the others.
