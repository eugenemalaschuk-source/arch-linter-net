# Policy Authoring Guide

This guide describes how AI agents should author ArchLinterNet policies safely.

## Start With Layers

Define layers from real namespace prefixes:

```yaml
layers:
  application:
    namespace: MyCompany.Product.Application
  domain:
    namespace: MyCompany.Product.Domain
```

Layer namespaces are prefix matches. `namespace_suffix` is available for
conventions such as `*.Contracts`, but layer definitions do not support regular
expressions or wildcards.

Prefer narrow layers before broad aggregate layers. If a repository has modules
such as `Sales`, `Billing`, and `Inventory`, model those modules directly before
adding a broad `application` layer that hides cross-module coupling.

## Choose Strict Or Audit

Use strict rules for current gates:

```yaml
contracts:
  strict:
    - name: domain-must-not-depend-on-infrastructure
      source: domain
      forbidden: [infrastructure]
      reason: Domain code must remain independent of infrastructure.
```

Use audit rules for migration discovery and future-state boundaries:

```yaml
contracts:
  audit:
    - name: audit-ui-bypassing-application
      source: ui
      forbidden: [domain]
      reason: Discover UI code that bypasses application use cases before making this strict.
```

Do not put known-failing future-state rules in strict unless the team explicitly
wants a blocking gate.

## Prefer Allow-Only For Pure Layers

Use `strict_allow_only` for pure layers where every first-party dependency
should be known:

```yaml
contracts:
  strict_allow_only:
    - name: domain-allowed-dependencies
      source: domain
      allowed: []
      reason: Domain must not depend on other first-party layers.
```

Allow-only contracts permit the source layer itself and the listed allowed
layers. `allowed_types` is an exact full type-name exception list, not a glob or
namespace rule.

## Use Ordered Layers Carefully

Layer order contracts list layers from outermost to innermost:

```yaml
contracts:
  strict_layers:
    - name: clean-architecture-layering
      layers:
        - ui
        - infrastructure
        - application
        - domain
      reason: Dependencies must point inward toward domain.
```

Do not mix parent aggregate layers and child layers in one ordered contract
unless each entry maps to a distinct namespace slice. Overlapping layers can make
diagnostics confusing.

## Model Modules With Independence Or Cycles

Use `strict_independence` when modules must not reference each other at all. Use
`strict_cycles` when cross-references may exist but directed cycles are not
allowed.

```yaml
contracts:
  strict_independence:
    - name: modules-must-be-independent
      layers: [sales, billing, inventory]
      reason: Bounded contexts communicate through explicit public contracts.
```

## Keep Ignores Narrow

`ignored_violations` is a frozen-debt baseline. Each entry should identify a
specific source type and forbidden reference, with a reason or issue link.

```yaml
ignored_violations:
  - source_type: MyCompany.Product.Application.Legacy.LegacyUseCase
    forbidden_reference: MyCompany.Product.Infrastructure.LegacyGateway
    reason: Existing migration debt tracked in #1234.
```

Avoid broad patterns such as `source_type: "*"` or
`forbidden_reference: "MyCompany.Product.Infrastructure.*"` unless a human has
explicitly accepted the debt baseline.

## Validate Before PR

Run strict validation for current gates and audit validation for migration
visibility:

```bash
arch-linter-net --policy architecture/dependencies.arch.yml --mode strict
arch-linter-net --policy architecture/dependencies.arch.yml --mode audit
```

When authoring with AI, also validate the YAML shape against
`schema/dependencies.arch.schema.json` because the current runtime loader ignores
unsupported fields.
