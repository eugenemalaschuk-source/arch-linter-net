## Context

ArchLinterNet currently supports 8 contract families (dependency, layer order, allow-only, cycle, method-body, asmdef, independence, protected surface). Each contract references named layers defined in the top-level `layers:` map. There is no mechanism to reuse a layer-ordering pattern across multiple namespace containers.

Issue [#11](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/11) proposes container-based layer templates inspired by Python Import Linter's `containers` + `layers` contract model.

## Goals / Non-Goals

**Goals:**
- Add `strict_layer_templates` / `audit_layer_templates` contract groups
- A template applies the same ordered layer contract to multiple namespace containers
- Relative layer names are resolved under each container via namespace prefixing
- Optional layers (`optional: true`) do not fail validation when absent
- Diagnostics identify the concrete container that violated the template
- JSON Schema and docs updated
- Existing contracts and layers unchanged

**Non-Goals:**
- Recursive sibling cycle detection across expanded containers
- Exhaustive container checks (e.g., warning when a container namespace has zero types across all layers)
- The `Application?` shorthand syntax (deferred to follow-up)

## Decisions

### 1. Template layer syntax: object form canonical

**Decision**: Use explicit `name` + `optional` objects instead of `?` suffix strings.

```yaml
# Canonical form
layers:
  - name: Presentation
  - name: Application
    optional: true
  - name: Domain
```

**Rationale**: Avoids a second parsing convention (`?` stripping) that every consumer (loader, schema, docs, AI manifest, diagnostics, examples) must understand. Explicit shape is safer for AI agents authoring policy.

The `?` shorthand may be added later; v1 uses the explicit form.

### 2. Separate contract group (not polymorphic `strict_layers`)

**Decision**: Templates live in their own `strict_layer_templates` / `audit_layer_templates` arrays, not as a variant of `strict_layers`.

**Rationale**: Clean JSON Schema (no conditional validation), no ambiguity or backwards-compatibility risk with existing `strict_layers`, simpler docs and AI manifest, and templates are conceptually a different authoring abstraction that expand into regular contracts.

### 3. Expansion in `ArchitectureValidator`, not loader or runner

**Decision**: A dedicated `LayerTemplateExpander` component is called by `ArchitectureValidator.Validate()` before passing concrete contracts to the runner.

```
Loader ──► Validator ──► LayerTemplateExpander ──► Runner
                              │
                          expands templates into
                          ArchitectureLayerContract
                          instances with:
                          - fully-qualified namespace layers
                          - OptionalLayers set
                          - TemplateName, ContainerNamespace metadata
```

**Rationale**: The loader should load declarative config only. The runner should receive concrete contracts only. The validator is the orchestration boundary — it loads, expands, then runs.

### 4. Expanded contract identity

**Decision**: Every expanded contract gets a deterministic composite ID and a human-readable name.

```
ID:   "feature-clean-architecture/firstice-features-fishing"
Name: "feature-clean-architecture (FirstIce.Features.Fishing)"
```

ID uses `NormalizeToContractId()` from the existing loader for consistency. This gives stable diagnostics, traceability back to the template, and works with the existing `--contract` selective execution filter.

### 5. Layer resolution: heuristic for direct-namespace vs named-layer

**Decision**: In `CheckLayerContract`, if a layer string contains a `.`, treat it as a direct namespace. Otherwise, resolve it as a named layer from `document.Layers`.

**Rationale**: This avoids new types or methods in the runner. Template-expanded contracts carry fully-qualified namespaces (e.g., `FirstIce.Features.Fishing.Presentation`), which naturally contain dots. Direct contracts carry single-word layer name references from the top-level `layers:` map. The heuristic is unambiguous for all realistic namespace patterns.

### 6. Optional layer handling

**Decision**: Optional layers that resolve to zero types are silently skipped from the effective layer list before directional checking.

```
For each layer in the contract:
  if types.Length == 0 AND layer is optional → skip (no diagnostic)
  if types.Length == 0 AND layer is required → report violation, skip
  otherwise → include in effective layer list

Then run standard directional check on effective layers only.
```

**Key distinction**: "Optional" means "may be absent" — if the layer IS present, it must still obey dependency direction.

### 7. Violation metadata

**Decision**: `ArchitectureViolation` gains two nullable init-only properties: `TemplateName` and `ContainerNamespace`. `CheckLayerContract` enriches violations from contract metadata using C# `with` expressions.

The diagnostic formatter includes these fields in JSON output. Human-readable output includes the template name in the contract name prefix.

### 8. Configuration check unchanged

**Decision**: `CheckConfiguration` is not modified. Template-expanded layers are not top-level `layers:` entries, so they are invisible to configuration checks. Required-vs-optional layer absence is handled entirely within `CheckLayerContract`.

### 9. Containers are raw namespace prefixes

**Decision**: The `containers` field holds raw namespace strings, not layer name references.

```yaml
containers:
  - FirstIce.Features.Fishing
```

Expansion: `$"{container}.{layerName}"` → `FirstIce.Features.Fishing.Presentation`

**Rationale**: Simpler, no indirection through named layers. Layer name references can be a follow-up if needed.

## Risks / Trade-offs

- **Risk**: The `.`-contains heuristic for direct-namespace detection could misclassify a top-level layer name that happens to contain a dot (extremely unlikely in practice). **Mitigation**: Top-level layer names in the `layers:` map are single-word identifiers (e.g., `core`, `cli`). If a dotted layer name were needed, it would be an explicit edge case.
- **Risk**: Large templates with many containers × many layers generate many expanded contracts, potentially overflowing CLI output. **Mitigation**: This is no different from writing equivalent contracts by hand — same number of checks, same runtime cost. No perf concern.
- **Trade-off**: The `containers` field uses raw namespace prefixes. If the same container namespace is defined as a top-level `layers:` entry, there is no collision issue — the template does not touch the top-level layer definitions. However, types in the container namespace itself (not in a sub-layer) are invisible to template checks. This is acceptable because templates define sub-namespace layers.
