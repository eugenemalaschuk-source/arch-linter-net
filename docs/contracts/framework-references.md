# Framework Reference Contracts

Framework reference contracts enforce rules over *declared* MSBuild `FrameworkReference` items (e.g. `Microsoft.AspNetCore.App`, `Microsoft.WindowsDesktop.App`) — independent of whether any compiled type actually references a type from that shared framework.

Two families are covered on this page:

- **Framework dependency** (`strict_framework_dependency`/`audit_framework_dependency`) — a source project/assembly must not declare a `FrameworkReference` matching any framework group in a `forbidden` list.
- **Framework allow-only** (`strict_framework_allow_only`/`audit_framework_allow_only`) — a source project/assembly may only declare `FrameworkReference`s matching framework groups in an `allowed` list; any other declared framework reference is a violation.

Framework groups (`framework_names`/`framework_name_prefixes`) are declared in a top-level `framework_references` section — see [framework references policy format](../policy-format/framework-references.md) for the YAML shape and matching rules.

## Framework dependency example

```yaml
framework_references:
  forbidden_web:
    framework_names: [Microsoft.AspNetCore.App]

contracts:
  strict_framework_dependency:
    - id: domain-no-aspnetcore
      name: domain-must-not-reference-aspnetcore
      source: MyApp.Domain
      forbidden: [forbidden_web]
      reason: Domain must stay free of the ASP.NET Core shared framework.
```

## Framework allow-only example

```yaml
framework_references:
  core_only:
    framework_names: [Microsoft.NETCore.App]

contracts:
  strict_framework_allow_only:
    - id: worker-allowed-frameworks
      name: worker-may-only-reference-core
      source: MyApp.Worker
      allowed: [core_only]
      reason: Worker projects may only reference the core shared framework.
```

The `source` of every framework dependency/allow-only contract must be listed in `analysis.target_assemblies`; a name that isn't a declared target assembly fails policy loading with an actionable error instead of silently being skipped.

## Semantics

Both families detect **direct, statically declared framework references only** — they parse each project's `.csproj` `FrameworkReference` items' `Include` and optional `Condition` attributes. `FrameworkReference` items carry no `Version` attribute, so there is no version resolution and no `dependency_depth` field — framework-reference governance has no transitive-dependency concept.

**Framework dependency**: for each framework group in `forbidden`, a violation is reported per matched `FrameworkReference`, aggregated into one violation per forbidden group naming every matched framework reference.

**Framework allow-only**: a violation is reported once per source, listing every declared `FrameworkReference` that does not match any framework group in `allowed`, sorted by framework name with duplicates removed.

Violations identify the source project/assembly, the forbidden framework group (for `framework_dependency`), and each matched framework reference by name; when the `FrameworkReference` item declares a `Condition`, the evidence includes that condition (e.g. `Microsoft.AspNetCore.App (Condition: '$(TargetFramework)'=='net10.0')`), so the same framework name declared under two different conditions in one project is distinguishable in output and baseline identity.

`ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families; here `source_type` holds the source assembly name and `forbidden_reference` holds the framework name.

## Configuration validation

Two safety checks run as part of `CheckConfiguration` (the same pass that already validates `packages`/`external_dependencies` group references), so misconfiguration surfaces as a visible failure instead of a contract silently matching nothing:

- **Unknown or unusable framework groups.** A framework group name referenced by a `forbidden`/`allowed` list that isn't declared in `framework_references`, or that is declared but has no non-empty `framework_names`/`framework_name_prefixes` matcher, is reported as an `unknown framework group`/`invalid framework group` configuration violation.
- **Missing project metadata for `source`.** If a contract's `source` does not correspond to any project discovered via `analysis.solution`/`analysis.projects` (including when project discovery never ran because neither is configured), a `no project metadata discovered` configuration violation names the contract and its source.

## Framework reference contracts vs package dependency contracts vs external dependency contracts

These checks operate at different boundaries and are independent of one another:

- **Framework reference dependency/allow-only contracts** (this page) check *declared MSBuild `FrameworkReference` items* — the shared-framework surface a project opts into, independent of `PackageReference`.
- **[Package dependency contracts](package-dependencies.md)** (`strict_package_dependency`/`audit_package_dependency`) check *declared NuGet package references*.
- **[External dependency contracts](external-dependencies.md)** (`strict_external`/`audit_external`) check *observed type references* to vendor/framework types in compiled code, including method-body IL scanning.

## Scope: what's not covered here

Only single-source contracts are supported in this release — reusable multi-source/glob authoring across many projects from one contract is a deferred follow-up. Detecting actual framework API usage in compiled code (Roslyn/semantic analysis) is out of scope; this is a pure declaration-level check. Implicit SDK-provided framework availability that isn't declared via an explicit `FrameworkReference` item is not governed.
