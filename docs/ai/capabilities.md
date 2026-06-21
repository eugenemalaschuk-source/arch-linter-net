# Capabilities

This page summarizes what ArchLinterNet can and cannot validate for AI policy
authors. The machine-readable version is `archlinternet.capabilities.json`.

## Supported Policy Structure

The policy file is usually `architecture/dependencies.arch.yml` and contains:

- `version`: current value is `1`.
- `name`: human-readable policy name.
- `layers`: named namespace-prefix or constrained glob layer definitions.
- `external_dependencies`: named vendor/framework dependency groups.
- `legacy_runtime_layers`: optional namespace prefixes used by dependency contracts.
- `analysis`: target assemblies, assembly search paths, optional source roots, condition sets, and default condition set.
- `contracts`: strict and audit contract groups.

## Supported Contract Families

| Family | Strict group | Audit group | Validates |
|--------|--------------|-------------|-----------|
| Dependency | `strict` | `audit` | Source layer must not reference forbidden layers |
| Layer order | `strict_layers` | `audit_layers` | Dependencies point from outer layers toward inner layers |
| Allow-only | `strict_allow_only` | `audit_allow_only` | Source layer references only itself and allowed first-party layers |
| Cycle | `strict_cycles` | `audit_cycles` | Selected layers do not form directed cycles |
| Acyclic sibling | `strict_acyclic_siblings` | `audit_acyclic_siblings` | Direct sibling namespaces under ancestor namespaces do not form dependency cycles |
| Method body | `strict_method_body` | `audit_method_body` | Source layer does not call forbidden APIs |
| asmdef | `strict_asmdef` | `audit_asmdef` | Unity `.asmdef` references avoid editor refs or forbidden prefixes |
| Independence | `strict_independence` | `audit_independence` | Selected layers do not reference each other |
| Protected surface | `strict_protected` | `audit_protected` | Protected layers are referenced only by explicitly allowed importers |
| External dependency | `strict_external` | `audit_external` | Source layer does not reference forbidden vendor/framework dependency groups |
| Layer template | `strict_layer_templates` | `audit_layer_templates` | Reusable layer order applied to multiple containers; optional layers skip empty namespaces; `exhaustive: true` detects unmapped sibling namespaces |
| Condition set | `analysis.condition_sets` | — | Named preprocessor symbol sets controlling which `#if` branches are active during Roslyn source/method-body scanning |

## Matching Semantics

Layer `namespace` values match exact namespaces and child namespaces. For
example, `MyCompany.Product.Domain` matches `MyCompany.Product.Domain` and
`MyCompany.Product.Domain.Models`.

Layer `namespace` also supports constrained glob patterns using `*` as a full
namespace segment. For example, `MyCompany.Product.Features.*` matches
`MyCompany.Product.Features.Audio` and descendants such as
`MyCompany.Product.Features.Audio.Player`.

`namespace_suffix` further requires the namespace to end with the suffix, such
as matching `*.Contracts` inside a broader namespace root. When `namespace`
contains a glob, `namespace_suffix` is position-fixed immediately after the
wildcard-resolved segment.

`allowed_types` entries are exact full type names.

`external_dependencies.namespace_prefixes` match exact namespaces and child
namespaces. `external_dependencies.type_prefixes` match full referenced type
names by prefix.

`ignored_violations` supports exact values and glob-like patterns for
`source_type` and `forbidden_reference`, but broad patterns should be treated as
temporary migration debt.

Method-body forbidden call patterns can match member names, `Type.Member`, fully
qualified members, or namespace prefixes ending with `.`.

Unity `.asmdef` scanning looks under `Assets` and treats assemblies with
`includePlatforms: ["Editor"]` as editor-only.

## Strict Versus Audit

Strict contracts are blocking rules for current no-new-debt gates. Audit
contracts are diagnostic rules for discovery, future-state architecture, and
migration planning.

The CLI and test adapter can run strict or audit mode. The lower-level
`ArchitectureValidator` API currently validates strict contracts only.

## Not Supported Yet

ArchLinterNet does not currently validate:

- Runtime behavior or dynamic dependency injection resolution.
- Security policy, authorization, or data access permissions.
- Code ownership or review ownership.
- Semantic data-flow analysis.
- Full method-body detection for external dependency contracts.
- Recursive `**`, `?`, character classes, raw regex, or partial-segment
  wildcard layer namespace definitions.
- Custom contract families outside the documented YAML schema.
- Automatic baseline generation.

Do not author YAML fields for unsupported capabilities. Track future needs in a
separate proposal instead.
