# Capabilities

This page summarizes what ArchLinterNet can and cannot validate for AI policy authors. The machine-readable version is `archlinternet.capabilities.json`.

## Supported policy structure

Each run uses one selected root policy; `architecture/arch.yml` is the
recommended concise convention, not a required filename. A root may import
focused local fragments and contains:

- `version`: current value is `1`.
- `name`: human-readable policy name.
- `imports`: optional ordered relative paths to partial policy fragments.
- `layers`: named namespace-prefix, constrained glob, or selector-backed layer definitions.
- `external_dependencies`: named vendor/framework dependency groups.
- `legacy_runtime_layers`: optional namespace prefixes used by dependency contracts.
- `analysis`: target assemblies, assembly search paths, optional source roots, condition sets, and default condition set.
- `contracts`: strict and audit contract groups.

## Supported contract families

| Family | Strict group | Audit group | Validates |
|--------|--------------|-------------|-----------|
| Dependency | `strict` | `audit` | Source layer must not reference forbidden layers. |
| Layer order | `strict_layers` | `audit_layers` | Dependencies point from outer layers toward inner layers. |
| Allow-only | `strict_allow_only` | `audit_allow_only` | Source layer references only itself and allowed first-party layers. |
| Cycle | `strict_cycles` | `audit_cycles` | Selected layers do not form directed cycles. |
| Acyclic sibling | `strict_acyclic_siblings` | `audit_acyclic_siblings` | Direct sibling namespaces under ancestor namespaces do not form dependency cycles. |
| Method body | `strict_method_body` | `audit_method_body` | Source layer does not call forbidden APIs. |
| asmdef | `strict_asmdef` | `audit_asmdef` | Unity `.asmdef` references avoid editor refs or forbidden prefixes. |
| Independence | `strict_independence` | `audit_independence` | Selected layers do not reference each other. |
| Assembly independence | `strict_assembly_independence` | `audit_assembly_independence` | Selected .NET assemblies do not directly reference each other. |
| Assembly dependency | `strict_assembly_dependency` | `audit_assembly_dependency` | Source assembly does not directly reference forbidden assemblies. |
| Assembly allow-only | `strict_assembly_allow_only` | `audit_assembly_allow_only` | Source assembly directly references only itself and explicitly allowed declared assemblies. |
| Project metadata | `strict_project_metadata` | `audit_project_metadata` | Selected discovered projects preserve required metadata, restrict friend assemblies, and avoid forbidden project references. |
| Protected surface | `strict_protected` | `audit_protected` | Protected layers are referenced only by explicitly allowed importers. |
| External dependency | `strict_external` | `audit_external` | Source layer does not reference forbidden vendor/framework dependency groups. |
| External allow-only | `strict_external_allow_only` | `audit_external_allow_only` | Source layer references only explicitly allowed vendor/framework dependency groups. |
| Layer template | `strict_layer_templates` | `audit_layer_templates` | Reusable layer order applied to multiple containers. |
| Type placement | `strict_type_placement` | `audit_type_placement` | A selected architectural role resides in a declared layer/namespace/project/assembly and/or carries a declared naming suffix/prefix. |
| Public API surface | `strict_public_api_surface` | `audit_public_api_surface` | An assembly's exported public/protected/protected-internal types and members match a declared signature allowlist. |
| Attribute usage | `strict_attribute_usage` | `audit_attribute_usage` | A declared attribute/marker type appears only in (or never in) a declared layer/namespace/project/assembly. |
| Inheritance | `strict_inheritance` | `audit_inheritance` | Types in a declared source layer/namespace do not inherit (directly or transitively) from declared forbidden base types. |
| Interface implementation | `strict_interface_implementation` | `audit_interface_implementation` | Implementations of declared interfaces reside only in (or never in) declared layers/namespaces/projects/assemblies. |
| Composition | `strict_composition` | `audit_composition` | Composition-root/service-locator API calls occur only from a declared composition boundary (layers/namespaces/projects/assemblies). |
| Coverage | `strict_coverage` | `audit_coverage` | First-party namespaces, projects, assemblies, dependency edges, rule inputs, and opt-in semantic roles are covered by a layer, template expansion, contextual selector, or explicit exclusion. |
| Contextual dependency | `strict_context_dependencies` | `audit_context_dependencies` | A source `(role, metadata)` selector's type must not reference a target matching a `forbidden` selector, compared directly against discovered role/metadata (no `layers.<name>` involved). |
| Contextual allow-only | `strict_context_allow_only` | `audit_context_allow_only` | A source `(role, metadata)` selector's type may reference only targets matching an `allowed` selector; same selector shape and operator vocabulary as contextual dependency. |
| Semantic port boundary | `strict_port_boundaries` | `audit_port_boundaries` | A selected source may reach a target context only through an explicit port or ACL seam; compiled adapter interface bindings can be checked. |

## Matching semantics

Layer `namespace` values match exact namespaces and child namespaces. For example, `MyCompany.Product.Domain` matches `MyCompany.Product.Domain` and `MyCompany.Product.Domain.Models`.

Layer `namespace` also supports constrained glob patterns using `*` as a full namespace segment. For example, `MyCompany.Product.Features.*` matches `MyCompany.Product.Features.Audio` and descendants such as `MyCompany.Product.Features.Audio.Player`.

`namespace_suffix` can narrow a layer to a suffix such as `Contracts` or `Models`. When `namespace` contains a glob, the suffix is position-fixed immediately after the resolved namespace pattern.

A layer may declare a `selector` (`role` and optional `metadata` key/value constraints) instead of, or in addition to, `namespace`. Selector matching uses the per-run role index (types classified via `classification.attributes`/`classification.assembly_attributes`) and applies exact-match AND semantics: every metadata key/value constraint must match. When both `namespace` and `selector` are present, a type must satisfy both. Selector-only layers produce types resolved by role/metadata alone, with no namespace predicate.

`external_dependencies.namespace_prefixes` match exact namespaces and child namespaces. `external_dependencies.type_prefixes` match full referenced type names by prefix.

`ignored_violations` should be exact and narrow. Broad patterns should be treated as temporary migration debt and reviewed by a human.

Type placement `types_matching` fields (`name_suffix`, `name_prefix`, `namespace`, `layer`, `base_type`, `implements_interface`, `has_attribute`) combine with AND semantics — every populated field must match. There is no regex or expression-language selector. `must_reside_in_projects` resolves to assembly-name matching via project discovery (see [Type placement contracts](../contracts/type-placement.md)); it is not physical `.csproj`-membership tracking.

Public API surface `declared_api` entries are normalized signature strings (`<kind> <FullyQualifiedName>[(<param types>)][: <member type>]`); generic type/method parameters are rendered positionally (`!N`/`!!N`), not by their source-declared name. `forbid_public_constants_unless_declared` is an independent, stricter check layered on top of the general declaration — an exported `const` field can still be forbidden even when its full signature is already in `declared_api`, unless its fully-qualified name is also in `allowed_public_constants`. See [Public API surface contracts](../contracts/public-api-surface.md) for the full grammar.

Attribute usage `attributes` entries match an attribute type's fully-qualified name exactly (ordinal); `attribute_prefixes` entries match by ordinal `StartsWith`. Every declared member is scanned regardless of visibility (unlike public API surface), because markers such as `[SerializeField]` and `[Authorize]` commonly decorate non-public members. `allowed_only_in_*` forms an allow-list (violation if the matched attribute's enclosing type satisfies none of it); `forbidden_in_*` forms a deny-list (violation if it satisfies any of it); a contract must declare at least one attribute selector and at least one location expectation. See [Attribute usage contracts](../contracts/attribute-usage.md) — this family does not implement required-marker ("must carry attribute X") checks; those are deferred to a documented follow-up.

Inheritance `forbidden_base_types` entries match a base type's fully-qualified name exactly (ordinal); `forbidden_base_type_prefixes` entries match by ordinal `StartsWith`. The full base-class chain is walked (transitive inheritance is detected), constructed generic base types are matched by their generic type definition's CLR name (arity suffix, e.g. `` App.Repository`1 ``), and interface implementations are never matched by this family. A contract must declare at least one source surface selector (`source_layers`/`source_namespaces`) and at least one base type selector. See [Inheritance contracts](../contracts/inheritance.md).

Interface implementation `interfaces` entries match an interface's fully-qualified name exactly (ordinal); `interface_prefixes` entries match by ordinal `StartsWith`. A non-interface type matches through its full interface set, including interfaces inherited via base classes; interface types extending a selected interface are never violations. `allowed_only_in_*`/`forbidden_in_*` follow the same allow-list/deny-list semantics as attribute usage, and a contract must declare at least one interface selector and at least one location expectation. This is static metadata analysis, not runtime dependency-injection resolution. See [Interface implementation contracts](../contracts/interface-implementation.md).

Composition `forbidden_apis` entries use the same call-pattern vocabulary as method-body contracts (member names, `Type.Member` names, fully qualified members, namespace/type prefixes). Every loaded type outside the `allowed_only_in_*` composition boundary is scanned reflection/IL-only for calls matching a `forbidden_apis` entry; a type inside the boundary is never scanned. A contract must declare at least one `forbidden_apis` entry and at least one `allowed_only_in_*` boundary entry — unlike interface implementation and attribute usage, there is no separate `forbidden_in_*` deny-list, since everything outside the allow-list is forbidden by definition. This is static reflection/IL call-site detection, not runtime dependency-injection resolution — it does not prove every service is registered correctly. See [Composition contracts](../contracts/composition.md).

Project metadata contracts target discovered project paths (`projects`) rather than assemblies or layers. `required_properties` and `forbidden_properties` compare exact scalar MSBuild property values, including statically inherited values from the nearest readable `Directory.Build.props` chain. `allowed_friend_assemblies` compares exact `InternalsVisibleTo` names from project-file items and source-level assembly attributes, and `forbidden_project_references` uses project-path glob matching against declared `ProjectReference` targets. This is static project metadata analysis only — not full MSBuild evaluation or runtime/package validation. See [Project metadata contracts](../contracts/project-metadata.md).

## Strict versus audit

Strict contracts are blocking rules for current no-new-debt gates. Audit contracts are diagnostic rules for discovery, future-state architecture, and migration planning.

Coverage findings also honor `analysis.coverage`:

- `error`: findings fail validation;
- `warn`: findings are reported but do not fail validation;
- `off`: findings are suppressed.

Current limit: `scope: namespace`, `scope: rule_input`, `scope: project`,
`scope: assembly`, `scope: dependency_edge`, and `scope: semantic_role` are implemented for coverage
contracts. `scope: dependency_edge` declares `between` (declared-layer-name
pairs) and classifies observed first-party namespace-to-namespace edges per
pair as `covered` (governed by an existing dependency, layer, independence,
allow-only, protected, or expanded layer-template contract), `excluded`, or
`uncovered`. Layer pairs absent from every `between` list are not evaluated.

## Supported adoption helpers

ArchLinterNet supports generated baselines for existing repositories:

```bash
arch-linter-net baseline generate \
  --config architecture/dependencies.arch.yml \
  --output architecture/baseline.arch.yml
```

Baselines are frozen debt, not a way to hide new violations.

## Not supported

ArchLinterNet does not currently validate:

- runtime behavior or dynamic dependency injection resolution;
- security policy, authorization, or data access permissions;
- code ownership or review ownership;
- semantic data-flow analysis;
- third-party package internals;
- unrestricted namespace pattern systems;
- unrestricted custom contract families outside the documented YAML schema;
- arbitrary YAML fields such as `severity`, `from`, `to`, `regex`, `owner`, or custom rule groups unless the schema documents them.

Do not author YAML fields for unsupported capabilities. Track future needs in a separate proposal instead.
