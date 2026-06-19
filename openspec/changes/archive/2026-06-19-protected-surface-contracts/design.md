## Context

ArchLinterNet has seven contract families, all source-oriented: forbidden dependencies, layer ordering, allow-only, cycles, method body, asmdef, and independence. None express "this layer is protected; only approved consumers may reference it." The missing target-oriented contract family makes internal-surface governance brittle — every consuming layer must opt-in individually.

The codebase follows a consistent pattern for each contract family: model class in `ArchitectureContractModels.cs`, strict/audit lists in `ArchitectureContractGroups`, accessor and check method in `ArchitectureContractRunner.cs`, iteration loop in `ArchitectureValidator.cs`, `ArchitectureAssertions.cs`, and `Program.cs`, duplicate ID group in `ArchitectureContractLoader.cs`, and schema entry in `dependencies.arch.schema.json`.

## Goals / Non-Goals

**Goals:**
- Add `strict_protected` / `audit_protected` contract groups with `ArchitectureProtectedContract` model
- Target layers listed in `protected`; approved importers listed in `allowed_importers`
- Edge-based enforcement: iterate dependency edges, flag any reference from non-allowed type to protected type
- Self-references within the protected layer implicitly allowed
- Support `allowed_types` (source-side type-level overrides) and `ignored_violations` (baselining)
- Structured JSON and human diagnostic output showing source type/layer, target type/layer, allowed importers, reason
- JSON Schema updated
- Self-architecture contract (`architecture/dependencies.arch.yml`) updated to protect Core internals
- Backward compatible: existing policy files unchanged, existing `ArchitectureViolation` positional constructor unchanged

**Non-Goals:**
- Ownership/reviewer policy
- Regex layer matching or glob patterns in `protected` / `allowed_importers` entry matching
- Performance benchmarking (document as future optimization opportunity)
- New CLI flags (reuses existing `--mode`, `--contract`)

## Decisions

### Decision 1: Edge-based enforcement over naive Cartesian product
**Choice**: Iterate all types in target assemblies, for each get references, check target namespace against protected layers.

**Rationale**: O(total dependency edges) rather than O(non-allowed types × protected layers). Avoids scanning types that have no connection to protected surfaces. Pre-built layer-name index makes layer resolution O(layers) per type — negligible for typical codebases.

**Alternatives considered**:
- *Naive N×M scan*: For each protected layer, scan all non-allowed types. Simpler code but O(N×M) complexity. Rejected for performance.
- *Reuse FindNamespaceViolations*: Would need to construct source types set by excluding allowed layers. Possible but requires type-level filtering across all assemblies anyway, so edge-based approach is more natural.

### Decision 2: `protected` as YAML key (not `layers`)
**Choice**: Use `protected` for the list of target layers.

**Rationale**: `ArchitectureDependencyContract` uses `forbidden` not `layers` for semantically meaningful lists. Following that pattern: `protected` reinforces the contract's purpose. `allowed_importers` is self-documenting.

### Decision 3: `allowed_types` reuses existing field name with source-side semantics
**Choice**: `allowed_types` in protected contracts exempts specific source types, mirroring how `allowed_types` exempts specific target types in forbidden contracts.

**Rationale**: In both cases `allowed_types` means "types exempt from this rule." The semantic role shifts (source-side vs target-side) but the exemption concept is the same. Preferring consistency over a new `allowed_importer_types` field keeps the schema simpler.

### Decision 4: Self-references automatically allowed
**Choice**: References within the protected layer are not reported, without needing the protected layer in `allowed_importers`.

**Rationale**: The purpose of protected contracts is to control the surface boundary, not internal code flow. Making users list the layer itself in `allowed_importers` is boilerplate.

### Decision 5: Extend `ArchitectureViolation` with optional init-only properties
**Choice**: Add `SourceLayer`, `TargetLayer`, and `AllowedImporters` as non-positional `init` properties on the record.

**Rationale**: Zero breakage of existing code. Positional constructor unchanged. Protected check populates extras. JSON formatter conditionally emits them.

**Alternatives considered**:
- *Subclass/special type*: Cleaner type safety but requires union handling in all consumers. Too much churn.
- *Encode into `ForbiddenNamespace` string*: Fragile for tools to parse. Rejected.
- *Change positional record*: Breaks all callers. Rejected.

### Decision 6: `allowed_importers` uses named layer keys only
**Choice**: No inline namespace prefixes, glob patterns, or assembly references.

**Rationale**: Matches existing contract conventions where all layer references are by key. Consistent, simple. One-off exceptions handled via `allowed_types` and `ignored_violations`.

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| Performance on large codebases (10K+ types) scanning every type's references | Edge-based approach is O(deps) not O(N×M). Pre-built layer index minimizes per-type cost. Document benchmark as future improvement. |
| `allowed_types` semantic inversion confuses users | Document explicitly: "For protected contracts, `allowed_types` exempts source types (importers), not target types." |
| `allowed_importers` cannot reference external layers | External layers are not in target assemblies, so their references to protected types are invisible anyway. Document this limitation. |
| Multiple protected contracts on overlapping layers produce duplicate violations | Each contract is evaluated independently. Duplicate violations are expected and match existing contract behavior. |
