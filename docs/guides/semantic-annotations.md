# Adopt semantic annotations

Semantic annotations give ArchLinterNet explicit role and context facts to analyze. They do not add architecture permissions or replace YAML policy. This guide shows the optional annotation path and the existing custom-attribute path.

> **Release availability:** `ArchLinterNet.Annotations` is prepared for stable v0.10.x releases and is omitted from v0.10 preview package sets and earlier releases. Use the package and an annotation-aware ArchLinterNet tool from the compatible v0.10.x line. Generation 1 supports package versions `[0.10.0,0.11.0)`; v0.9.1 tools do not recognize these canonical identities. See the [annotation catalog](../reference/semantic-annotation-catalog.md) for the complete compatibility details.

## Start with a few stable roles

The package is optional and source-only. It contributes C# source to each project that directly references it; it adds no annotation runtime DLL. Add a direct reference to every project that declares canonical annotations. Central Package Management can centralize the version, but does not replace the direct project reference.

```xml
<ItemGroup>
  <PackageReference Include="ArchLinterNet.Annotations" />
</ItemGroup>
```

Set its version through your repository's normal central package or project version metadata, and choose a version from the compatible range in the [catalog](../reference/semantic-annotation-catalog.md). Projects without annotations do not need the package.

Annotate only concepts that have stable meaning in your codebase. The injected attribute types are internal to the authoring project, so adding them does not make your types public or change their CLR visibility.

```csharp
using ArchLinterNet.Annotations;

namespace Acme.Sales.Domain;

[AggregateRoot(BoundedContext = "Sales", Module = "Orders")]
public sealed class OrderAggregate
{
}

[ValueObject(BoundedContext = "Sales", Module = "Orders")]
public sealed record Money(decimal Amount, string Currency);
```

`AggregateRoot` and `ValueObject` are each a type's one primary semantic role. `BoundedContext` and `Module` add context metadata to that role. Do not add a second role annotation to satisfy a different policy rule; author a selector for the fact that expresses the actual intent.

For a modular layered project, an assembly can provide a default layer role while selected types carry their more specific primary role. These roles do not accumulate on one type: a type-level role takes precedence over an assembly-level role.

```csharp
using ArchLinterNet.Annotations;

[assembly: DomainLayer]
```

Other types in the same assembly without a more specific role can be classified as `DomainLayer`; `OrderAggregate` remains `AggregateRoot` as shown above.

## Inspect facts, then author policy

For ordinary .NET projects, make sure the policy's `analysis.solution` or `analysis.projects` includes each annotated authoring project, then opt into building and verifying that selected graph before validation. `--ensure-built` is explicit; validation does not silently rebuild:

```bash
arch-linter-net --policy architecture/dependencies.arch.yml --mode audit --format json --ensure-built
```

The JSON report includes discovered facts in `classification_roles` and reports catalog compatibility problems in `canonical_annotation_diagnostics`. If the selected graph is already restored and the build must stay offline, add `--no-restore`.

**Unity projects have a separate build step.** Compile or import scripts with Unity first; `--ensure-built` builds the selected .NET project graph and does not invoke Unity's compiler. Point `analysis.target_assemblies` and `analysis.assembly_search_paths` at the freshly compiled Unity assemblies before validating them. Recompile in Unity after changing annotated source.

Use the report to confirm the subject, winning role, metadata, and evidence before changing policy. A compatible annotation creates classification evidence; YAML still decides which evidence a layer, contract, or coverage rule governs.

For example, select Sales aggregates explicitly and opt into semantic-role coverage when the repository is ready to account for classified and unclassified types:

```yaml
layers:
  sales_aggregates:
    namespace: Acme.Sales.Domain
    selector:
      role: AggregateRoot
      metadata:
        boundedContext: Sales

contracts:
  strict_coverage:
    - id: semantic-role-coverage
      name: semantic-role-coverage
      scope: semantic_role
      reason: Every discovered semantic fact must be governed or explicitly excluded.
```

Coverage is opt-in. It reports unclassified first-party types, classified facts that no selector or implemented contextual contract governs, stale selectors, and classification conflicts separately from dependency violations. Review the findings before making a selector broader.

The command `explain --source <id> --target <id>` explains a dependency path under policy. Use validation's `classification_roles` output to inspect the discovered semantic facts; explain does not replace that inventory.

## Keep custom attributes first-class

Use your own attributes and exact full-name YAML mappings when they express your vocabulary better, when the role is outside the bounded canonical set, when you need metadata the canonical attributes do not support, or when a repository must not reference an ArchLinterNet package.

```csharp
using System;

namespace Acme.Architecture;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class BusinessAggregateAttribute : Attribute
{
}

[BusinessAggregate]
public sealed class CustomerAccount
{
}
```

Map the user-owned identity in YAML. Assembly attributes can be mapped separately when they carry assembly-scoped role or metadata evidence:

```yaml
classification:
  attributes:
    - attribute: Acme.Architecture.BusinessAggregateAttribute
      role: AggregateRoot
      metadata:
        boundedContext: Sales
```

The custom path remains available for both `classification.attributes` and
`classification.assembly_attributes`; see [metadata extraction syntax](../policy-format/semantic-classification.md#metadata-extraction-syntax)
and [classification precedence](../policy-format/semantic-classification.md#precedence)
for constructor metadata and source precedence rules. Do not map names in the
reserved `ArchLinterNet.Annotations` namespace: canonical identities are
recognized by exact FQN and reserved identities cannot be remapped.

## Migrate without changing behavior

There is no requirement to replace a working custom mapping. If a canonical annotation exactly matches an existing mapping's intent, migrate incrementally:

1. Save the current validation report and its `classification_roles` facts.
1. Add the compatible package directly to each project that will author annotations.
1. Annotate a small set of types with the same primary role and intended context values.
1. Run the same tool and policy again; compare the affected subjects' roles and metadata before and after.
1. Preserve custom metadata intentionally. Canonical attributes support only the bounded keys listed in the [catalog](../reference/semantic-annotation-catalog.md); other metadata can remain in a custom mapping.
1. Remove a redundant mapping only after canonical classification parity is established. Keep it when it still expresses distinct intent.

Do not combine annotation migration with unrelated policy weakening, broader selectors, new ignores, or visibility changes. If the facts differ, keep the current mapping and resolve the difference separately.

## Roles, context, visibility, and API membership

These are different facts with different owners:

| Fact | What it means | What supplies it |
| --- | --- | --- |
| Primary semantic role | One architectural role for a type, such as `ValueObject`, `Entity`, or `Controller` | Canonical annotation, custom mapping, inheritance, or another implemented classifier |
| Context metadata | Values such as `domain`, `boundedContext`, `module`, `feature`, `platform`, or `runtime` | Role metadata properties, context-only annotations, or explicit YAML mapping |
| CLR visibility | Whether a declaration is `public`, `internal`, or otherwise visible in .NET | C# declaration and assembly rules; annotations do not change it |
| Reviewed API membership | Whether an exported type is intentionally included in a reviewed API snapshot | The authored `strict_public_api_surface` or `audit_public_api_surface` selector and snapshot |

`ApiContract` is a primary role for types whose role is genuinely an API contract. It does not select public API membership. A `ValueObject` remains a `ValueObject` when a separate, user-owned marker selects it into the reviewed snapshot:

```csharp
[ValueObject(BoundedContext = "Sales")]
[PublicApiContract] // user-owned marker for the reviewed API selector
public sealed record Money(decimal Amount, string Currency);
```

```yaml
contracts:
  strict_public_api_surface:
    - id: sales-public-api
      name: sales-public-api-reviewed
      assemblies: [Acme.Sales.Contracts]
      surface_selector:
        has_attribute: Acme.Sales.Architecture.PublicApiContractAttribute
      api_snapshot: architecture/api/sales-public-api.txt
      api_comparison: exact
      reason: Only deliberately marked exported types belong to this reviewed surface.
```

The marker controls membership only; it does not overwrite the type's role. CLR `public` visibility alone also does not mean that a type is part of a reviewed snapshot. See [public API surface contracts](../contracts/public-api-surface.md) for selector and snapshot behavior.

## Context metadata conflicts

Role metadata and context-only attributes can both supply the same context key. If they disagree at the same scope, ArchLinterNet reports a deterministic metadata conflict and omits that key while preserving the otherwise valid primary role:

```csharp
[AggregateRoot(BoundedContext = "Sales")]
[BoundedContext("Inventory")]
public sealed class OrderAggregate
{
}
```

Choose one value for the type, or place a deliberate default on the assembly and let a type-level value override it. Equal values coalesce. Different values at the same scope are not resolved by source order.

## Representative role families

The same single-role and evidence-only rules apply across the catalog:

```csharp
using ArchLinterNet.Annotations;

// CQRS and API
[Command(BoundedContext = "Sales", Feature = "Checkout")]
public sealed record PlaceOrder(string OrderId);

[CommandHandler(BoundedContext = "Sales", Feature = "Checkout")]
public sealed class PlaceOrderHandler
{
}

[Controller(BoundedContext = "Sales")]
public sealed class OrdersController
{
}

// Infrastructure adapter: one role describes the adapter's primary role.
[Adapter(Platform = "PostgreSQL", BoundedContext = "Sales")]
public sealed class PostgresOrderStore
{
}

// Unity/client: labels static architectural intent and does not prove runtime behavior.
[MonoBehaviourAdapter(Platform = "Unity", Runtime = "player")]
public sealed class InventoryViewBridge
{
}
```

Layer roles such as `DomainLayer` and `ApplicationLayer` are assembly-targeted; `CompositionRoot` supports types and assemblies. Unity assembly roles such as `UnityRuntime` and `UnityEditor` are also assembly-targeted. For Unity source-import workflows, the annotation source supports C# 9.0; projects that cannot consume NuGet content files can import all reviewed `.cs` files from the package's `contentFiles/cs/any/` folder into each authoring assembly. This does not guarantee that every Unity project supports NuGet `contentFiles` installation. The annotations do not inspect framework inheritance, dependency injection, serialized scenes, or runtime behavior.

See the [complete canonical annotation catalog](../reference/semantic-annotation-catalog.md) for every identity, target, metadata key, and compatibility detail.
