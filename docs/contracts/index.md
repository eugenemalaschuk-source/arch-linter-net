# Contract Families

This index is the reviewed human projection of `archlinternet.capabilities.json`. The hidden `contract-family` markers are checked by `make lint-docs`; adding or removing a machine capability without updating this page fails the documentation gate.

Strict groups are blocking. Audit groups use the same family semantics for non-blocking migration/discovery.

| Family | Strict / audit groups | Purpose |
| --- | --- | --- |
<!-- contract-family: dependency -->
| [Dependency](dependency.md) | `strict` / `audit` | Forbid dependencies from one layer to selected target layers. |
<!-- contract-family: layer-order -->
| [Layer order](layers.md) | `strict_layers` / `audit_layers` | Enforce an ordered inward dependency direction. |
<!-- contract-family: allow-only -->
| [Allow-only](allow-only.md) | `strict_allow_only` / `audit_allow_only` | Restrict a layer to itself plus explicitly allowed first-party layers. |
<!-- contract-family: cycle -->
| [Cycle](cycles.md) | `strict_cycles` / `audit_cycles` | Detect directed cycles among selected layers. |
<!-- contract-family: acyclic-sibling -->
| [Acyclic sibling](acyclic-siblings.md) | `strict_acyclic_siblings` / `audit_acyclic_siblings` | Keep direct sibling namespaces below selected ancestors acyclic. |
<!-- contract-family: module-container -->
| [Module container](module-container.md) | `strict_module_containers` / `audit_module_containers` | Discover direct feature modules and enforce a reviewed module profile. |
<!-- contract-family: method-body -->
| [Method body](method-body.md) | `strict_method_body` / `audit_method_body` | Forbid selected API calls using source/IL analysis. |
<!-- contract-family: asmdef -->
| [Unity asmdef](asmdef.md) | `strict_asmdef` / `audit_asmdef` | Govern Unity `.asmdef` references. |
<!-- contract-family: independence -->
| [Independence](independence.md) | `strict_independence` / `audit_independence` | Keep selected layers mutually independent. |
<!-- contract-family: assembly-independence -->
| [Assembly independence](assembly-independence.md) | `strict_assembly_independence` / `audit_assembly_independence` | Keep selected .NET assemblies mutually independent. |
<!-- contract-family: assembly-dependency -->
| [Assembly dependency](assembly-dependency.md) | `strict_assembly_dependency` / `audit_assembly_dependency` | Forbid direct first-party assembly references. |
<!-- contract-family: assembly-allow-only -->
| [Assembly allow-only](assembly-dependency.md) | `strict_assembly_allow_only` / `audit_assembly_allow_only` | Restrict direct first-party assembly references to an allow-list. |
<!-- contract-family: project-metadata -->
| [Project metadata](project-metadata.md) | `strict_project_metadata` / `audit_project_metadata` | Govern project properties, friend assemblies, and project references. |
<!-- contract-family: protected-surface -->
| [Protected surface](protected-surface.md) | `strict_protected` / `audit_protected` | Restrict which layers may import a protected layer. |
<!-- contract-family: external-dependency -->
| [External dependency](external-dependencies.md) | `strict_external` / `audit_external` | Forbid vendor/framework namespace/type dependency groups. |
<!-- contract-family: external-allow-only -->
| [External allow-only](external-allow-only.md) | `strict_external_allow_only` / `audit_external_allow_only` | Restrict external dependencies to reviewed groups. |
<!-- contract-family: layer-template -->
| [Layer template](layer-templates.md) | `strict_layer_templates` / `audit_layer_templates` | Apply reusable layer order to multiple namespace containers. |
<!-- contract-family: type-placement -->
| [Type placement](type-placement.md) | `strict_type_placement` / `audit_type_placement` | Constrain matching types by location and naming. |
<!-- contract-family: layout-conventions -->
| [Layout conventions](layout-conventions.md) | `strict_layout_conventions` / `audit_layout_conventions` | Govern source-file/declaration layout conventions. |
<!-- contract-family: public-api-surface -->
| [Public API surface](public-api-surface.md) | `strict_public_api_surface` / `audit_public_api_surface` | Govern exported API signatures and reviewed snapshots. |
<!-- contract-family: attribute-usage -->
| [Attribute usage](attribute-usage.md) | `strict_attribute_usage` / `audit_attribute_usage` | Restrict where selected attributes may appear. |
<!-- contract-family: inheritance -->
| [Inheritance](inheritance.md) | `strict_inheritance` / `audit_inheritance` | Forbid selected base classes in selected source surfaces. |
<!-- contract-family: interface-implementation -->
| [Interface implementation](interface-implementation.md) | `strict_interface_implementation` / `audit_interface_implementation` | Restrict where implementations of selected interfaces may live. |
<!-- contract-family: composition -->
| [Composition](composition.md) | `strict_composition` / `audit_composition` | Restrict composition/service-locator calls to reviewed boundaries. |
<!-- contract-family: coverage -->
| [Coverage](coverage.md) | `strict_coverage` / `audit_coverage` | Govern namespace, project, assembly, dependency-edge, rule-input, and semantic-role coverage. |
<!-- contract-family: context-dependency -->
| [Contextual dependency](context-dependency.md) | `strict_context_dependencies` / `audit_context_dependencies` | Forbid dependencies between semantic role/metadata contexts. |
<!-- contract-family: context-allow-only -->
| [Contextual allow-only](context-allow-only.md) | `strict_context_allow_only` / `audit_context_allow_only` | Allow semantic-context dependencies only to selected contexts. |
<!-- contract-family: port-boundary -->
| [Semantic port boundary](port-boundary.md) | `strict_port_boundaries` / `audit_port_boundaries` | Require selected context crossings to pass through a port/ACL seam. |
<!-- contract-family: package-dependency -->
| [Package dependency](package-dependencies.md) | `strict_package_dependency` / `audit_package_dependency` | Forbid selected NuGet package groups from project/assembly sources. |
<!-- contract-family: package-allow-only -->
| [Package allow-only](package-dependencies.md) | `strict_package_allow_only` / `audit_package_allow_only` | Restrict NuGet references to reviewed package groups. |
<!-- contract-family: framework-dependency -->
| [Framework dependency](package-dependencies.md) | `strict_framework_dependency` / `audit_framework_dependency` | Forbid selected MSBuild `FrameworkReference` groups. |
<!-- contract-family: framework-allow-only -->
| [Framework allow-only](package-dependencies.md) | `strict_framework_allow_only` / `audit_framework_allow_only` | Restrict framework references to reviewed groups. |

## Choosing a family

Use the narrowest contract that expresses the architectural decision:

- Choose dependency/allow-only contracts for named layer relationships.
- Choose contextual contracts when the rule is naturally role/metadata based.
- Choose a port boundary when a cross-context dependency is allowed only through an explicit seam.
- Choose package/framework/project families for MSBuild/project governance rather than approximating those facts with namespaces.
- Choose coverage in addition to behavioral rules so newly discovered architecture cannot remain ungoverned.
- Use audit mode for migration discovery; promote to strict only when the rule is ready to block.

For reusable inputs across many contracts, see `source_sets` in the [Policy format](../policy-format/index.md).
