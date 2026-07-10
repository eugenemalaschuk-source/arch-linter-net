## Why

ArchLinterNet's architecture model is entirely namespace-driven today: every layer, dependency, independence, and protected contract identifies "what code is in this layer" only through `layers.<name>.namespace`/`namespace_suffix` glob matching. Story #106 wants code itself — attributes, assembly metadata, inheritance, and conventions — to be able to imply an architectural role, so YAML selectors and future contextual contracts (#111/#112/#173) can validate boundaries between business contexts without hand-maintaining namespace lists for every module. Before any extraction, indexing, selector-matching, or contextual-contract engine is implemented (#108–#114), the classification vocabulary and YAML schema must be reviewed and stable, exactly as #96 did for the coverage model before #97–#103 implemented it. This change is that design step for semantic role discovery.

## What Changes

- Define the semantic-classification vocabulary: `role`, `metadata`, `source`, `evidence`, `confidence`/`precedence`, `conflict`, `override`, `exclusion`, `stale selector`, and `uncovered semantic fact`.
- Design a new top-level `classification` YAML section (sibling to `layers`/`analysis`/`contracts`) covering: source precedence, type-attribute mappings, assembly-attribute mappings, inheritance mappings, namespace/path convention mappings, explicit overrides, and exclusions.
- Design `layers.<name>.selector` as a new optional field on the existing `layer` shape, additive alongside `namespace`/`namespace_suffix`/`external` — `namespace`'s own required-ness, meaning, and matching behavior are unchanged for any policy that keeps using it.
- Define a deterministic, fixed six-tier source precedence (`yaml_override > type_attribute > assembly_attribute > inheritance > namespace > path`) and same-tier conflict resolution (first-declared-wins).
- Define a small, deterministic metadata-extraction syntax (`constructor[<index>]`, `property:<Name>`, `const:<Full.Type.NAME>`, or a literal scalar) — no reflection DSL, no regex.
- Define selector syntax (`role` + optional exact-match `metadata`) reusing existing glob matchers for namespace/path sources — no new pattern language.
- Define override/exclusion syntax, including exactly when `reason` is required.
- Explain — without implementing — how semantic classification will extend the existing #96 coverage vocabulary (a future `scope: semantic_role` coverage variant, owned by #114) rather than inventing a parallel diagnostic system.
- Update `schema/dependencies.arch.schema.json` to accept the reviewed shape, additive only.
- Document explicit non-goals and the product boundary (no engine, no annotation package, no contextual contracts, no runtime behavior change).

## Capabilities

### New Capabilities
- `semantic-classification-model`: The classification vocabulary, `classification` YAML shape, `layers.<name>.selector` shape, precedence/conflict rules, metadata-extraction syntax, override/exclusion rules, and coverage-integration design that #108–#114 implement against. This change defines the *shape*, not the *engine* — no attribute is read, no role is ever assigned, and no selector ever matches at runtime as a result of this change.

### Modified Capabilities
_None._ No existing capability's requirements change; `layers.<name>.namespace` keeps its current required-ness relaxed only to accommodate the new alternative (`selector`), not altered in meaning.

## Impact

- `schema/dependencies.arch.schema.json`: new `$defs` for the `classification` section and its sub-shapes, a new `selector` `$def`, a relaxed `required` on the existing `layer` `$def` (`namespace` OR `selector`, additive), and a new `classification` property on the schema root. Additive only — no existing field's shape or meaning changes.
- `tests/ArchLinterNet.Core.Tests/ArchitectureContractSchemaTests.cs`: new regression tests asserting the new `$defs`/properties exist, following the existing lightweight schema-assertion pattern (no engine, no binding).
- No changes to `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` or any runtime/validation code path — this change intentionally introduces no C# binding and no runtime guard (see design.md Decision on this deviation from the #96 coverage-model precedent).
- `docs/`: new design reference for the classification model (vocabulary, YAML shape, worked examples).
- No change to runtime validation behavior for any existing policy — a policy with no `classification` section and no `layers.<name>.selector` field behaves identically to today.
