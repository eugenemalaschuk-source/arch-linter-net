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

Both families detect **direct, statically declared framework references only** — but resolve them through a **real, per-target-framework MSBuild design-time build** (via Buildalyzer), not raw XML parsing. This means:

- `Condition` on both the `FrameworkReference` item itself and its containing `ItemGroup` is evaluated exactly as `dotnet build` would evaluate it, separately for each of the project's configured target frameworks.
- Declarations contributed by imported `.props`/`.targets` files (including `Directory.Build.props` and SDK-injected targets) are included, since MSBuild's own evaluation processes them.
- Every discovered reference is classified **explicit** (authored by the project or one of its imports) or **implicit** (SDK-injected, e.g. `Microsoft.NETCore.App`, via MSBuild's `IsImplicitlyDefined` item metadata).

`FrameworkReference` items carry no `Version` attribute, so there is no version resolution and no `dependency_depth` field — framework-reference governance has no transitive-dependency concept.

**Framework dependency**: for each framework group in `forbidden`, a violation is reported per matched `FrameworkReference`, aggregated into one violation per forbidden group naming every matched framework reference.

**Framework allow-only**: a violation is reported once per source, listing every declared `FrameworkReference` that does not match any framework group in `allowed`, sorted by framework name with duplicates removed.

Violations identify the source project/assembly, the forbidden framework group (for `framework_dependency`), and each matched framework reference by name and its real evaluated target framework (e.g. `Microsoft.AspNetCore.App (net10.0)`), so the same framework name applicable to two different target frameworks in one multi-targeted project is distinguishable in output and baseline identity. Structured evidence — framework name, target framework, explicit/implicit classification, and the declaring project's path — is carried separately from the human-formatted string, for JSON/SARIF/Testing API consumers.

`ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families; here `source_type` holds the source assembly name and `forbidden_reference` holds the framework name.

## Configuration validation

Three safety checks run as part of `CheckConfiguration` (the same pass that already validates `packages`/`external_dependencies` group references), so misconfiguration or an evaluation failure surfaces as a visible failure instead of a contract silently matching nothing:

- **Unknown or unusable framework groups.** A framework group name referenced by a `forbidden`/`allowed` list that isn't declared in `framework_references`, or that is declared but has no non-empty `framework_names`/`framework_name_prefixes` matcher, is reported as an `unknown framework group`/`invalid framework group` configuration violation.
- **Missing project metadata for `source`.** If a contract's `source` does not correspond to any project discovered via `analysis.solution`/`analysis.projects` (including when project discovery never ran because neither is configured), a `no project metadata discovered` configuration violation names the contract and its source.
- **MSBuild evaluation failure (fail closed).** If the source project's real MSBuild design-time build does not succeed — for the whole project, or for any one of its configured target frameworks (e.g. that target framework isn't installed) — a `framework reference evaluation failed` configuration violation names the contract, the source, and which target framework could not be evaluated. The contract's own check never reports a false-clean result on the basis of data it could not actually evaluate.

## Framework reference contracts vs package dependency contracts vs external dependency contracts

These checks operate at different boundaries and are independent of one another:

- **Framework reference dependency/allow-only contracts** (this page) check *declared MSBuild `FrameworkReference` items* — the shared-framework surface a project opts into, independent of `PackageReference`.
- **[Package dependency contracts](package-dependencies.md)** (`strict_package_dependency`/`audit_package_dependency`) check *declared NuGet package references*.
- **[External dependency contracts](external-dependencies.md)** (`strict_external`/`audit_external`) check *observed type references* to vendor/framework types in compiled code, including method-body IL scanning.

## Scope: what's not covered here

Only single-source contracts are supported in this release — reusable multi-source/glob authoring across many projects from one contract is a deferred follow-up. Detecting actual framework API usage in compiled code (Roslyn/semantic analysis) is out of scope; this is a pure declaration-level check. Implicit SDK-provided framework availability that isn't declared via an explicit `FrameworkReference` item is not governed.
