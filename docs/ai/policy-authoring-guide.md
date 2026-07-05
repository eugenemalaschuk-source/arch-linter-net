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
conventions such as `*.Contracts`.

Layer definitions also support a constrained `*` wildcard when it occupies a
whole namespace segment:

```yaml
layers:
  feature_modules:
    namespace: MyCompany.Product.Features.*

  feature_contracts:
    namespace: MyCompany.Product.Features.*
    namespace_suffix: Contracts
```

Use this only when you need one layer to cover repeated sibling namespaces.

Rules:

- `*` matches exactly one namespace segment.
- Descendants under the resolved prefix still match.
- With `namespace_suffix`, the suffix is position-fixed immediately after the
  full resolved namespace pattern.
- `*` must be a full segment. Do not author `Feature*`, `*Feature`, or `F*eature`.
- Do not author `**`, `?`, character classes, or regex.

Examples:

- `MyCompany.Product.Features.*` matches `MyCompany.Product.Features.Audio` and
  `MyCompany.Product.Features.Audio.Player`.
- `namespace: MyCompany.Product.Features.*` with
  `namespace_suffix: Contracts` matches
  `MyCompany.Product.Features.Audio.Contracts` and
  `MyCompany.Product.Features.Audio.Contracts.Dto`.
- That same pattern does not match
  `MyCompany.Product.Features.Audio.Internal.Contracts`.

Prefer narrow layers before broad aggregate layers. If a repository has modules
such as `Sales`, `Billing`, and `Inventory`, model those modules directly before
adding a broad `application` layer that hides cross-module coupling. Use glob
layers as aggregate views, not as a replacement for the concrete layers you need
for specific contracts and diagnostics.

## Choose Strict Or Audit

Use strict rules for current gates. Add an `id` for stable CLI and CI references:

```yaml
contracts:
  strict:
    - id: domain-not-infrastructure
      name: domain-must-not-depend-on-infrastructure
      source: domain
      forbidden: [infrastructure]
      reason: Domain code must remain independent of infrastructure.
```

Use audit rules for migration discovery and future-state boundaries:

```yaml
contracts:
  audit:
    - id: audit-ui-to-domain
      name: audit-ui-bypassing-application
      source: ui
      forbidden: [domain]
      reason: Discover UI code that bypasses application use cases before making this strict.
```

When `id` is omitted it is derived automatically from `name` (lowercased with
hyphens). Explicit `id` values are recommended for stable references in CI and
AI-agent workflows.

Do not put known-failing future-state rules in strict unless the team explicitly
wants a blocking gate.

## Use External Dependencies For Vendor Or Framework Leakage

When the target is not a first-party layer but a vendor/framework surface such
as Unity, EF Core, or a cloud SDK, model it with `external_dependencies` and
`strict_external` / `audit_external` instead of inventing pseudo-layers:

```yaml
external_dependencies:
  unity_runtime:
    namespace_prefixes:
      - UnityEngine
    type_prefixes: []

contracts:
  strict_external:
    - id: core-no-unity
      name: core-must-not-reference-unity
      source: core
      forbidden: [unity_runtime]
      reason: Pure core must not expose Unity runtime types.
```

Use `external: true` on a layer only when you intentionally want layer-style
semantics with missing-type suppression. For new vendor/framework controls,
prefer `external_dependencies`.

External dependency contracts detect forbidden references through type-level
metadata (base types, interfaces, fields, properties, method signatures,
generic arguments) and method-body IL scanning (method calls, constructor
calls, field/property access, type references inside method bodies). They do
not analyze third-party package internals. This is static reference analysis,
not semantic data-flow or runtime validation.

## Use Transitive Depth For Indirect Coupling

When a dependency should be blocked at any depth (direct or indirect), use
`dependency_depth: transitive`. This follows the type dependency graph via BFS
and reports violations with full path diagnostics:

```yaml
contracts:
  strict:
    - id: cli-not-transitively-testing
      name: cli-must-not-transitively-depend-on-testing
      source: cli
      forbidden: [testing]
      dependency_depth: transitive
      reason: CLI must not have any transitive dependency path into Testing.
```

Transitive mode is more expensive than direct mode. Use it when auditing
indirect coupling across module boundaries. The default is `direct`, which
checks only immediate type references.

## Prefer Allow-Only For Pure Layers

Use `strict_allow_only` for pure layers where every first-party dependency
should be known:

```yaml
contracts:
  strict_allow_only:
    - id: domain-pure
      name: domain-allowed-dependencies
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
    - id: clean-layering
      name: clean-architecture-layering
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

## Use Layer Templates For Repeated Shapes

When multiple modules or features share the same internal architecture, use
`strict_layer_templates` instead of duplicating ordered-layer contracts:

```yaml
contracts:
  strict_layer_templates:
    - name: feature-clean-architecture
      containers:
        - MyApp.Features.Fishing
        - MyApp.Features.Inventory
        - MyApp.Features.Map
      layers:
        - name: Presentation
        - name: Application
          optional: true
        - name: Domain
      reason: Every feature follows the same internal dependency direction.
```

Each `containers` entry is a raw namespace prefix — layer names are resolved by
prepending the container. For container `MyApp.Features.Fishing`, the template
above produces layers `[MyApp.Features.Fishing.Presentation, ...]`.

Optional layers (`optional: true`) produce no diagnostic when absent. If present,
they must still obey the dependency direction.

Use `audit_layer_templates` for audit-mode templates. Templates coexist with
direct `strict_layers` / `audit_layers` contracts.

### Exhaustive container coverage

When a template declares `exhaustive: true`, the runner verifies that every
immediate child namespace under each container that contains loaded types is
mapped to a declared layer. Any unmapped sibling namespace produces a violation.

This catches new modules added under an existing container root without
corresponding layer declarations — a common governance gap in growing codebases.

```yaml
contracts:
  strict_layer_templates:
    - name: feature-clean-architecture
      containers:
        - MyApp.Features.Fishing
        - MyApp.Features.Inventory
        - MyApp.Features.Map
      layers:
        - name: Presentation
        - name: Application
        - name: Domain
      exhaustive: true
      reason: Every feature must declare all internal layers; new modules must not silently bypass the architecture.
```

When `exhaustive: true`, template layer names must be single namespace segments
(e.g. `Domain`, not `Domain.Models`). The layer name is prepended to the
container to form the full namespace, so dotted names would produce a namespace
deeper than an immediate child and cannot be validated correctly.

Only namespaces that contain at least one loadable type are checked. Empty
child namespaces are silently ignored.

Exhaustive works in both strict and audit modes. Use strict for blocking gates
and audit for discovery. The check only runs on expanded template contracts
(with a `ContainerNamespace`), not on direct layer contracts.

## Model Modules With Independence Or Cycles

Use `strict_independence` when modules must not reference each other at all. Use
`strict_cycles` when cross-references may exist but directed cycles are not
allowed.

```yaml
contracts:
  strict_independence:
    - id: modules-independent
      name: modules-must-be-independent
      layers: [sales, billing, inventory]
      reason: Bounded contexts communicate through explicit public contracts.
```

Use `strict_assembly_independence` when the boundary you need to enforce is a
compiled .NET assembly rather than a namespace/layer — for example, feature
assemblies or plugin packages whose ownership doesn't map cleanly onto
namespace prefixes. Every assembly listed must also appear in
`analysis.target_assemblies`. Detection is direct-reference-only.

```yaml
contracts:
  strict_assembly_independence:
    - id: feature-assemblies-independent
      name: feature-assemblies-must-remain-independent
      assemblies: [MyApp.Features.Billing, MyApp.Features.Shipping]
      reason: Feature assemblies must not directly reference each other.
```

This is a different mechanism from `strict_independence` (namespace-based) and
from Unity `strict_asmdef`/`audit_asmdef` (Unity `.asmdef` manifest checks) —
see [Assembly independence contracts](../contracts/assembly-independence.md)
for the distinction.

Use `strict_assembly_dependency` when the boundary you need is directional and
assembly-scoped — for example, `MyApp.Domain` must never reference
`MyApp.Infrastructure`. Use `strict_assembly_allow_only` when a source assembly
should only reference an explicit allow-list of other declared assemblies —
for example, an application assembly that may depend on abstractions but not
concrete adapters. Both are direct-reference-only, and every assembly name
referenced (`source`, `forbidden`, `allowed`) must appear in
`analysis.target_assemblies`. Both accept an optional `dependency_depth` field
that only supports `direct` (the default) in this release — do not author
`dependency_depth: transitive` for these two families; it fails policy loading
with an actionable error rather than being silently ignored.

```yaml
contracts:
  strict_assembly_dependency:
    - id: domain-no-infrastructure
      name: domain-must-not-reference-infrastructure
      source: MyApp.Domain
      forbidden: [MyApp.Infrastructure]
      reason: Domain must stay free of infrastructure concerns.
  strict_assembly_allow_only:
    - id: application-allowed-refs
      name: application-may-only-reference-abstractions
      source: MyApp.Application
      allowed: [MyApp.Domain, MyApp.Domain.Abstractions]
      reason: Application may depend on abstractions, not concrete adapters.
```

These are different from `strict_assembly_independence` (mutual, not
directional) — see [Assembly dependency contracts](../contracts/assembly-dependency.md)
for the distinction.

Use `strict_acyclic_siblings` when you want to automatically discover sibling
namespaces under one or more ancestor namespaces and ensure they don't form
dependency cycles. This is useful for feature-group architectures where siblings
are added over time without updating policy definitions.

```yaml
contracts:
  strict_acyclic_siblings:
    - id: features-acyclic
      name: feature-siblings-must-be-acyclic
      ancestors:
        - MyApp.Features
        - MyApp.Modules
      reason: New feature siblings should not introduce cycles.
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

When `analysis.unmatched_ignored_violations` is enabled (default `error`), the
linter warns about `ignored_violations` entries that match no current violation.
Remove stale entries proactively to keep the baseline trustworthy and avoid CI
failures. Use `warn` during migration cleanup, then switch to `error`.

## Policy Consistency Checks

Separately from scanning code, the linter always runs a policy-consistency
pass over the policy document itself, looking for internal contradictions:
duplicate contract IDs (including those produced by layer-template
expansion), allow-only contracts that conflict with a forbidding contract for
the same layer pair, independence contracts contradicted by an explicit
allowed dependency, protected-surface `allowed_importers` that conflict with
a strict forbidding rule, overlapping internal layer definitions, and
contracts that reference a structurally unreachable layer. `analysis.policy_consistency`
(default `error`) controls whether these findings fail validation (`error`),
are reported without failing (`warn`), or are suppressed entirely (`off`).

## Use Automated Baselines For Existing Codebases

When adding architecture rules to an existing codebase with existing violations,
use the automated baseline generation workflow instead of hand-writing
`ignored_violations` entries:

```bash
arch-linter-net baseline generate \
  --config architecture/dependencies.arch.yml \
  --output baseline.yml \
  --reason "Initial baseline"
```

Then validate with the baseline:

```bash
arch-linter-net --policy architecture/dependencies.arch.yml \
  --baseline baseline.yml --mode strict
```

The baseline file is a separate YAML file that is merged into the policy's
ignores at runtime. This keeps the policy file clean and makes the baseline
lifecycle explicit — entries are added by the generator and removed as
violations are fixed. See [Migration Baselines](../guides/migration-baselines.md)
for the full lifecycle.

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
