# Contracts

ArchLinterNet supports several contract families. Each family has a **strict** variant
(blocks the build) and an **audit** variant (reports violations without blocking).

## Dependency (`strict` / `audit`)

Checks that a source layer does not reference forbidden layers.

```yaml
strict:
  - name: app-must-not-depend-on-infrastructure
    source: app
    forbidden: [infrastructure]
    reason: Application should not depend on infrastructure directly.
```

## Layer Order (`strict_layers` / `audit_layers`)

Import-linter-style inward-only layering constraints. Layers are ordered from outermost
to innermost; each layer may only depend on layers below it.

```yaml
strict_layers:
  - name: clean-architecture-layers
    layers:
      - infrastructure
      - app
      - domain
    reason: Dependencies must point inward toward the domain.
```

## Allow-Only (`strict_allow_only` / `audit_allow_only`)

Whitelist model: a source layer may reference only the explicitly allowed layers.
Any reference outside the allow list is a violation.

```yaml
strict_allow_only:
  - name: web-only-allowed-dependencies
    source: web
    allowed: [application, infrastructure]
    reason: Web layer may only depend on application and infrastructure.
```

## Cycle (`strict_cycles` / `audit_cycles`)

Detects directed cycles among a set of layers. Any cycle between the specified layers
is reported.

```yaml
strict_cycles:
  - name: no-cycles-between-main-layers
    layers:
      - app
      - domain
      - infrastructure
    reason: Core layers must not form dependency cycles.
```

## Method-Body (`strict_method_body` / `audit_method_body`)

Detects forbidden calls inside method bodies using Roslyn semantic symbol resolution
from source, with IL token fallback scanning from compiled assemblies.

```yaml
strict_method_body:
  - name: domain-must-not-call-repositories
    source: domain
    forbidden_calls:
      - Microsoft.EntityFrameworkCore.DbContext
      - MyApp.Infrastructure.IRepository
    reason: Domain layer must not use infrastructure APIs.
```

## asmdef (`strict_asmdef` / `audit_asmdef`)

Validates Unity assembly definition (`.asmdef`) dependency boundaries.

```yaml
strict_asmdef:
  - name: gameplay-asmdef-boundaries
    source_assemblies: [Gameplay]
    forbidden_editor_refs: true
    reason: Gameplay assembly must not reference editor assemblies.
```

## Layer Template (`strict_layer_templates` / `audit_layer_templates`)

Reusable layer order templates that apply the same layered architecture to
multiple namespace containers. Each template expands into one concrete layer
order contract per container, with relative layer names resolved by prepending
the container namespace.

```yaml
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

Optional layers (`optional: true`) are silently skipped when absent. If present,
they must still obey the dependency direction. Required layers with zero types
produce a configuration violation.

## Independence (`strict_independence` / `audit_independence`)

Mutual separation across a set of layers: no cross-references in either direction.

```yaml
strict_independence:
  - name: bounded-contexts-separation
    layers:
      - billing
      - shipping
      - notifications
    reason: Bounded contexts must remain independent of each other.
```

## Protected Surface (`strict_protected` / `audit_protected`)

Target-side protection: a protected layer may only be referenced by explicitly
allowed importer layers. Any reference from outside the allow list is a violation.
This is the inverse of the dependency contract — instead of "source A must not
reference target B", it answers "target B is internal; who may reference it?"

```yaml
strict_protected:
  - name: core-internals-are-protected
    protected: [core_internal]
    allowed_importers: [core]
    reason: External layers must use the public Core surface only.
```

Self-references within the protected layer are implicitly allowed. Type-level
exceptions are supported via `allowed_types` and violation baselining via
`ignored_violations`. Protected contracts work with both `strict` and `audit`
modes, enabling gradual adoption in existing codebases.
