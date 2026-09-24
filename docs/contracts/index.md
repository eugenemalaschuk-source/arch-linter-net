# Contract Families

Choose the narrowest rule that expresses the architectural decision. Strict
and audit collections select different contract sets. A standalone audit
validation can return a failing exit code; make its CI step explicitly advisory
when migration findings should not block. See [exit codes](../usage/exit-codes.md).

<!-- contract-family: dependency -->
<!-- contract-family: layer-order -->
<!-- contract-family: allow-only -->
<!-- contract-family: cycle -->
<!-- contract-family: acyclic-sibling -->
<!-- contract-family: module-container -->
<!-- contract-family: method-body -->
<!-- contract-family: asmdef -->
<!-- contract-family: independence -->
<!-- contract-family: assembly-independence -->
<!-- contract-family: assembly-dependency -->
<!-- contract-family: assembly-allow-only -->
<!-- contract-family: project-metadata -->
<!-- contract-family: protected-surface -->
<!-- contract-family: external-dependency -->
<!-- contract-family: external-allow-only -->
<!-- contract-family: layer-template -->
<!-- contract-family: type-placement -->
<!-- contract-family: layout-conventions -->
<!-- contract-family: layout-convention-applicability -->
<!-- contract-family: public-api-surface -->
<!-- contract-family: contract-surface-exposure -->
<!-- contract-family: versioned-contract-surface-isolation -->
<!-- contract-family: attribute-usage -->
<!-- contract-family: inheritance -->
<!-- contract-family: interface-implementation -->
<!-- contract-family: composition -->
<!-- contract-family: coverage -->
<!-- contract-family: metric-budget -->
<!-- contract-family: context-dependency -->
<!-- contract-family: context-allow-only -->
<!-- contract-family: port-boundary -->
<!-- contract-family: package-dependency -->
<!-- contract-family: package-allow-only -->
<!-- contract-family: framework-dependency -->
<!-- contract-family: framework-allow-only -->

| Family | Strict / audit groups | Purpose |
| --- | --- | --- |
| [Dependency](dependency.md) | `strict` / `audit` | Forbid selected layer dependencies. |
| [Layer order](layers.md) | `strict_layers` / `audit_layers` | Require dependencies to point inward. |
| [Allow-only](allow-only.md) | `strict_allow_only` / `audit_allow_only` | Restrict allowed first-party layers. |
| [Cycle](cycles.md) | `strict_cycles` / `audit_cycles` | Detect directed layer cycles. |
| [Acyclic sibling](acyclic-siblings.md) | `strict_acyclic_siblings` / `audit_acyclic_siblings` | Keep sibling namespaces acyclic. |
| [Module container](module-container.md) | `strict_module_containers` / `audit_module_containers` | Apply a reviewed profile to discovered modules. |
| [Method body](method-body.md) | `strict_method_body` / `audit_method_body` | Forbid selected calls through source/IL analysis. |
| [Unity asmdef](asmdef.md) | `strict_asmdef` / `audit_asmdef` | Govern Unity manifest references. |
| [Independence](independence.md) | `strict_independence` / `audit_independence` | Keep selected layers independent. |
| [Assembly independence](assembly-independence.md) | `strict_assembly_independence` / `audit_assembly_independence` | Keep assemblies independent. |
| [Assembly dependency](assembly-dependency.md) | `strict_assembly_dependency` / `audit_assembly_dependency` | Forbid direct assembly references. |
| [Assembly allow-only](assembly-dependency.md) | `strict_assembly_allow_only` / `audit_assembly_allow_only` | Restrict direct assembly references. |
| [Project metadata](project-metadata.md) | `strict_project_metadata` / `audit_project_metadata` | Govern properties, friend assemblies and project references. |
| [Protected surface](protected-surface.md) | `strict_protected` / `audit_protected` | Restrict importers of a protected layer. |
| [External dependency](external-dependencies.md) | `strict_external` / `audit_external` | Forbid vendor/framework type dependencies. |
| [External allow-only](external-allow-only.md) | `strict_external_allow_only` / `audit_external_allow_only` | Restrict external dependency groups. |
| [Layer template](layer-templates.md) | `strict_layer_templates` / `audit_layer_templates` | Reuse layer order across namespace containers. |
| [Type placement](type-placement.md) | `strict_type_placement` / `audit_type_placement` | Constrain type locations and naming. |
| [Layout conventions](layout-conventions.md) | `strict_layout_conventions` / `audit_layout_conventions` | Govern source-file/declaration layout. |
| [Layout applicability](layout-conventions.md#applicability-inventory) | `strict_layout_convention_applicability` / `audit_layout_convention_applicability` | Detect stale folders and incomplete selectors. |
| [Public API surface](public-api-surface.md) | `strict_public_api_surface` / `audit_public_api_surface` | Govern selected exported signatures and snapshots. |
| [Contract-surface exposure](contract-surface-exposure.md) | `strict_contract_surface_exposure` / `audit_contract_surface_exposure` | Prevent visible signatures from exposing forbidden types. |
| [Version isolation](versioned-contract-surface-isolation.md) | `strict_versioned_contract_surface_isolation` / `audit_versioned_contract_surface_isolation` | Isolate versioned or implementation surfaces. |
| [Attribute usage](attribute-usage.md) | `strict_attribute_usage` / `audit_attribute_usage` | Restrict where markers may appear. |
| [Inheritance](inheritance.md) | `strict_inheritance` / `audit_inheritance` | Forbid selected base classes. |
| [Interface implementation](interface-implementation.md) | `strict_interface_implementation` / `audit_interface_implementation` | Restrict interface implementations. |
| [Composition](composition.md) | `strict_composition` / `audit_composition` | Restrict composition/service-locator calls. |
| [Coverage](coverage.md) | `strict_coverage` / `audit_coverage` | Detect gaps in governed namespaces, projects, assemblies, edges, inputs and roles. |
| [Metric budget](../policy-format/architecture-metrics.md) | `strict_metric_budgets` / `audit_metric_budgets` | Enforce absolute or baseline-relative metric budgets. |
| [Contextual dependency](context-dependency.md) | `strict_context_dependencies` / `audit_context_dependencies` | Forbid semantic-context dependencies. |
| [Contextual allow-only](context-allow-only.md) | `strict_context_allow_only` / `audit_context_allow_only` | Restrict allowed semantic-context dependencies. |
| [Semantic port boundary](port-boundary.md) | `strict_port_boundaries` / `audit_port_boundaries` | Require a port/ACL seam for selected context crossings. |
| [Package dependency](package-dependencies.md) | `strict_package_dependency` / `audit_package_dependency` | Forbid selected NuGet groups. |
| [Package allow-only](package-dependencies.md) | `strict_package_allow_only` / `audit_package_allow_only` | Restrict NuGet groups. |
| [Framework dependency](package-dependencies.md) | `strict_framework_dependency` / `audit_framework_dependency` | Forbid selected FrameworkReference groups. |
| [Framework allow-only](package-dependencies.md) | `strict_framework_allow_only` / `audit_framework_allow_only` | Restrict FrameworkReference groups. |

## Choosing a family

Use namespace/layer rules for code dependencies and project/package/framework
rules for build metadata. A visible API signature leaking an implementation type
needs contract-surface exposure, not merely a package rule. Use contextual rules
when the boundary is expressed by semantic role or metadata, and a port boundary
when the crossing must use an explicit seam.

Add coverage so new subjects cannot escape the selected rules. Reuse
`source_sets` where contracts deliberately share an input universe; they do not
expand the analysis scope. See [Policy format](../policy-format/index.md).

The inventory markers above are checked against `archlinternet.capabilities.json`.
They are outside the table so both the site and repository view render its rows.
