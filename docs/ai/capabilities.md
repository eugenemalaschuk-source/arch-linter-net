# Capabilities

This page summarizes what ArchLinterNet can and cannot validate for AI policy authors. The machine-readable version is `archlinternet.capabilities.json`.

## Supported policy structure

The policy file is usually `architecture/dependencies.arch.yml` and contains:

- `version`: current value is `1`.
- `name`: human-readable policy name.
- `layers`: named namespace-prefix or constrained glob layer definitions.
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
| Protected surface | `strict_protected` | `audit_protected` | Protected layers are referenced only by explicitly allowed importers. |
| External dependency | `strict_external` | `audit_external` | Source layer does not reference forbidden vendor/framework dependency groups. |
| Layer template | `strict_layer_templates` | `audit_layer_templates` | Reusable layer order applied to multiple containers. |
| Coverage | `strict_coverage` | `audit_coverage` | First-party namespaces, discovered projects, and resolved assemblies are covered by a layer, template expansion, or explicit exclusion. |

## Matching semantics

Layer `namespace` values match exact namespaces and child namespaces. For example, `MyCompany.Product.Domain` matches `MyCompany.Product.Domain` and `MyCompany.Product.Domain.Models`.

Layer `namespace` also supports constrained glob patterns using `*` as a full namespace segment. For example, `MyCompany.Product.Features.*` matches `MyCompany.Product.Features.Audio` and descendants such as `MyCompany.Product.Features.Audio.Player`.

`namespace_suffix` can narrow a layer to a suffix such as `Contracts` or `Models`. When `namespace` contains a glob, the suffix is position-fixed immediately after the resolved namespace pattern.

`external_dependencies.namespace_prefixes` match exact namespaces and child namespaces. `external_dependencies.type_prefixes` match full referenced type names by prefix.

`ignored_violations` should be exact and narrow. Broad patterns should be treated as temporary migration debt and reviewed by a human.

## Strict versus audit

Strict contracts are blocking rules for current no-new-debt gates. Audit contracts are diagnostic rules for discovery, future-state architecture, and migration planning.

Coverage findings also honor `analysis.coverage`:

- `error`: findings fail validation;
- `warn`: findings are reported but do not fail validation;
- `off`: findings are suppressed.

Current limit: `scope: namespace`, `scope: rule_input`, `scope: project`, and
`scope: assembly` are implemented for coverage contracts. `scope: dependency_edge`
remains reserved and must fail validation if authored today.

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
