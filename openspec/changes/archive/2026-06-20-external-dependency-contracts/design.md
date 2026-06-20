## Context

ArchLinterNet already supports first-party layer dependency contracts and an `external: true` layer flag. The flag is useful as a compatibility escape hatch because it suppresses empty-layer configuration diagnostics for namespaces whose assemblies may not be present, but it still treats a vendor/framework namespace as a layer.

Issue #15 needs a clearer model: external dependencies are dependency surfaces such as Unity, Entity Framework, cloud SDKs, or payment SDKs. They should be declared separately from first-party layers and referenced by dedicated contracts so policies can express "Core must not reference Unity" without pretending `UnityEngine` is part of the project's layer graph.

The current reference scanner is reflection/type-metadata based. It reliably observes references that are exposed through loadable type metadata such as base types, interfaces, fields, properties, method signatures, return types, parameters, and generic arguments. It does not promise exhaustive method-body usage detection, and unresolved external assemblies may prevent referenced type names from being observed.

## Goals / Non-Goals

**Goals:**
- Introduce first-class `external_dependencies` declarations with namespace and type prefix matching.
- Introduce `strict_external` and `audit_external` contracts that forbid external dependency groups from source layers.
- Keep the MVP direct, deterministic, and aligned with the current type/reference graph.
- Report diagnostics that identify source type, contract, forbidden external dependency group, and matched reference.
- Keep strict/audit separation consistent with existing validation behavior.
- Preserve `external: true` layers for backward compatibility.

**Non-Goals:**
- Do not perform full method-body analysis for arbitrary calls such as `Debug.Log`, `new StripeClient(...)`, or `dbContext.SaveChanges()` unless those usages are already visible through the existing reference scanner path.
- Do not statically analyze third-party package internals.
- Do not guarantee detection when external assemblies are unavailable or unresolved enough that referenced type metadata cannot be read.
- Do not change global CLI audit-mode exit semantics.
- Do not replace existing layer dependency contracts or remove `external: true` layer support.

## Decisions

### Add a separate external dependency model

Add a top-level policy section:

```yaml
external_dependencies:
  unity_runtime:
    namespace_prefixes:
      - UnityEngine
    type_prefixes: []

  infrastructure_sdks:
    namespace_prefixes:
      - Amazon
      - Azure
      - Microsoft.EntityFrameworkCore
    type_prefixes:
      - Stripe.StripeClient
```

Rationale: external dependency groups are not first-party layers. Keeping them separate prevents external surfaces from polluting layer ordering, cycle, independence, and protected-surface semantics.

Alternative considered: continue using `external: true` layers. This is smaller but does not provide first-class groups, external-specific diagnostics, or clear guidance for vendor/framework leakage.

### Add a dedicated contract family

Add `strict_external` and `audit_external` under `contracts`:

```yaml
contracts:
  strict_external:
    - id: core-no-unity
      name: core-must-not-reference-unity
      source: game_core
      forbidden:
        - unity_runtime
        - unity_editor
      reason: Pure core must not reference Unity runtime or editor APIs.
```

Rationale: a dedicated family makes strict/audit execution, contract selection, schema validation, diagnostics, and AI policy authoring explicit. It avoids overloading the existing layer-to-layer dependency contract with mixed first-party and external semantics.

Alternative considered: add `forbidden_external` to existing dependency contracts. This reuses existing wiring but creates conditional semantics inside a contract currently centered on layer names.

### Match by namespace and type prefixes over observed references

External groups match referenced type metadata using exact-or-child namespace prefix semantics and full type-name prefix semantics. For example, `UnityEngine` matches `UnityEngine.Vector3` and `UnityEngine.Rendering.RenderPipeline`, while `Stripe.StripeClient` matches that type and nested or suffixed full-name forms if exposed by runtime metadata.

Rationale: namespace prefixes cover common framework/vendor package surfaces, while type prefixes allow targeted SDK entry points. The matcher can reuse the existing scanner's referenced `Type` names without scanning external assemblies internally.

### Keep external contracts direct-only for MVP

External contracts should check direct references observed from source types. They should not introduce a custom external transitive graph in this change.

Rationale: external leakage is most predictable when reported at the first-party type that directly exposes or depends on the vendor/framework type. Transitive external paths can be considered later if real projects need them.

### Preserve current audit semantics

Strict validation evaluates `strict_external` and fails on its violations. Audit validation evaluates `audit_external` and reports its violations using the existing audit-mode behavior. A strict run does not fail because of audit external contracts.

Rationale: this satisfies issue #15 without changing global CLI behavior for audit mode.

## Risks / Trade-offs

- Users may expect method-body detection for calls such as `Debug.Log` -> Document the MVP boundary clearly and consider a later method-body external contract family.
- Missing external assemblies may hide references -> Document that references must be resolvable enough for the current scanner to observe type names.
- Diagnostics could break existing JSON consumers if fields are renamed -> Add external-specific fields while preserving existing `forbidden_namespace` and `forbidden_references` shape where practical.
- `ArchitectureContractRunner.cs` is already large -> Prefer factoring matcher/check logic into small focused Core classes instead of growing the runner substantially.
- `external: true` layers and `external_dependencies` may confuse users -> Keep compatibility, but make docs recommend `external_dependencies` for new vendor/framework rules.
