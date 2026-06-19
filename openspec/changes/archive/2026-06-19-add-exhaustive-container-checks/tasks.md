## 1. Model

- [x] 1.1 Add `Exhaustive` property to `ArchitectureLayerTemplateContract` in `ArchitectureContractModels.cs`
- [x] 1.2 Add `Exhaustive` property to `ArchitectureLayerContract` in `ArchitectureContractModels.cs`

## 2. Expander

- [x] 2.1 Propagate `Exhaustive` from template to expanded contract in `LayerTemplateExpander.Expand()`

## 3. Runner — Exhaustive sibling check

- [x] 3.1 Add `FindChildNamespaces()` helper to `ArchitectureContractRunner`
- [x] 3.2 Add exhaustive sibling namespace check in `CheckLayerContract()` after direction check

## 4. Schema

- [x] 4.1 Add `exhaustive` boolean to `layerTemplateContract` in `schema/dependencies.arch.schema.json`

## 5. Tests

- [x] 5.1 Add expander test: `Expand_ExhaustiveTemplate_SetsExhaustiveFlag`
- [x] 5.2 Add runner test: `CheckLayerContract_Exhaustive_AllChildrenMapped_NoViolation`
- [x] 5.3 Add runner test: `CheckLayerContract_Exhaustive_UnmappedSibling_ProducesViolation`
- [x] 5.4 Add runner test: `CheckLayerContract_Exhaustive_UnmappedSiblingWithoutTypes_Silent`
- [x] 5.5 Add runner test: `CheckLayerContract_NonExhaustive_UnmappedSibling_Silent`

## 6. Documentation

- [x] 6.1 Update `docs/contracts/index.md` with exhaustive example
- [x] 6.2 Update OpenSpec `openspec/specs/layer-templates/spec.md` with exhaustive requirements (sync from change delta)

## 7. Verification

- [x] 7.1 Run `rtk make acceptance` to verify all tests pass
