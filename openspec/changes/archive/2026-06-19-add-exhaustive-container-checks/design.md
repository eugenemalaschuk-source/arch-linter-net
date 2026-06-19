## Context

The arch-linter-net project supports layer templates that expand a reusable layer pattern across multiple namespace containers. The current system validates directional dependency ordering within each container, but does not check for **unmapped sibling namespaces** — new namespaces created under a container root that are not listed in the template's layers.

Current flow:
1. `ArchitectureLayerTemplateContract` defines `containers` + `layers` in YAML
2. `LayerTemplateExpander.Expand()` produces `ArchitectureLayerContract` per container
3. `ArchitectureContractRunner.CheckLayerContract()` validates direction + empty layers
4. Violations carry `TemplateName` and `ContainerNamespace` metadata

The gap: a developer creates `MyApp.Features.Fishing.Payments` under container `MyApp.Features.Fishing`, but the template only lists `Presentation`, `Application`, `Domain`. The new namespace silently escapes the architecture map.

## Goals / Non-Goals

**Goals:**
- Detect sibling namespaces under a container root that are not mapped into any declared layer
- Support `exhaustive` flag on layer template contracts (opt-in, default false)
- Integrate into existing violation reporting (human-readable + JSON)
- Backward compatible — existing non-exhaustive templates unchanged

**Non-Goals:**
- Auto-generating architecture policy from source folders
- Replacing all layer contracts with container templates
- Recursive deep-namespace detection (only immediate children)
- Performance optimizations beyond correctness

## Decisions

### 1. Check location: `CheckLayerContract` in the runner

**Decision**: Add the exhaustive sibling check inside `ArchitectureContractRunner.CheckLayerContract()`, after the existing direction check.

**Rationale**: The runner already has access to loaded assemblies via `_context.TargetAssemblies` and handles the `ContainerNamespace` metadata. Placing the check here keeps the responsibility boundary clean — the expander only does namespace expansion, the runner does all assembly-based validation.

**Alternative considered**: Checking during expansion in `LayerTemplateExpander.Expand()`. Rejected because the expander has no access to loaded assemblies.

### 2. Detection method: immediate child namespaces from type scanning

**Decision**: Scan all types under `ContainerNamespace`, extract their namespaces, and keep only namespaces that are exactly one dot-level deeper than the container. Any such namespace not matching a declared layer is unmapped.

**Example**: Container `MyApp.Features.Fishing`, declared layers `Presentation`, `Application`, `Domain`. Types found in:
- `MyApp.Features.Fishing.Presentation.Services` → maps to layer `Presentation` ✓
- `MyApp.Features.Fishing.Domain` → maps to layer `Domain` ✓
- `MyApp.Features.Fishing.Payments` → no match → unmapped violation ✗

**Rationale**: One-dot-level children are the natural "sibling" concept. Deeper nesting is already covered by the parent layer's namespace prefix match.

**Alternative considered**: Recursive namespace tree walking. Rejected as overly broad — nested namespaces are usually sub-layers of their parent and shouldn't be independently mapped.

### 3. Empty namespace handling: silent

**Decision**: Unmapped child namespaces with zero types produce no violation. Only namespaces containing actual types trigger a diagnostic.

**Rationale**: Empty placeholder namespaces (e.g., `MyApp.Features.Fishing.Shared` with no types yet) are common and should not block CI. This matches the existing optional layer behavior.

### 4. Model propagation: `Exhaustive` flag on expanded contract

**Decision**: Add `Exhaustive` as a `YamlIgnore` property on `ArchitectureLayerContract`, set by the expander from `ArchitectureLayerTemplateContract.Exhaustive`. The runner checks this flag to decide whether to run the sibling scan.

**Rationale**: The runner only sees expanded `ArchitectureLayerContract` instances. Carrying the flag through the expander avoids passing the original templates to the runner.

### 5. YAML model: `exhaustive` boolean on template contract

**Decision**: Add `[YamlMember(Alias = "exhaustive")] public bool Exhaustive { get; set; }` to `ArchitectureLayerTemplateContract`. Default false (backward compatible).

**Rationale**: Clean opt-in semantics. The existing YAML shape is preserved with one additive property.

## Risks / Trade-offs

- **False positives from implicit namespaces**: A namespace might exist as a side effect of nested generic types or compiler-generated code. → Mitigation: only flag namespaces with actual user types (already handled by `FindTypesInNamespace`).

- **External assemblies**: The exhaustive check only sees types from loaded `TargetAssemblies`. Namespaces in unloaded assemblies are invisible. → Mitigation: this is consistent with all other contract checks; users must list all relevant assemblies in `target_assemblies`.

- **Performance**: Scanning all types under a container for namespace extraction is O(n) in the number of types. For typical .NET projects this is negligible. → No mitigation needed for correctness-first design.
