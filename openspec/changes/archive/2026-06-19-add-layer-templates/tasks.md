## 1. Model Layer

- [x] 1.1 Add `ArchitectureTemplateLayer` and `ArchitectureLayerTemplateContract` classes to `ArchitectureContractModels.cs`
- [x] 1.2 Add `StrictLayerTemplates` and `AuditLayerTemplates` properties to `ArchitectureContractGroups`
- [x] 1.3 Add `OptionalLayers` (`HashSet<string>`, `[YamlIgnore]`), `TemplateName`, and `ContainerNamespace` to `ArchitectureLayerContract`
- [x] 1.4 Add `TemplateName` and `ContainerNamespace` as nullable init-only properties to `ArchitectureViolation` record

## 2. Loading & Expansion

- [x] 2.1 Update `ArchitectureContractLoader` to validate template contract IDs (normalize, detect duplicates)
- [x] 2.2 Create `LayerTemplateExpander` class that transforms templates into concrete `ArchitectureLayerContract` instances with fully-qualified namespace layers
- [x] 2.3 Wire `LayerTemplateExpander` into `ArchitectureValidator.Validate()` — expand both strict and audit templates, merge with direct contracts before passing to runner

## 3. Validation & Diagnostics

- [x] 3.1 Modify `ArchitectureContractRunner.CheckLayerContract` to detect direct-namespace layers (contain `.`) vs named-layer lookups
- [x] 3.2 Add optional layer filtering in `CheckLayerContract` — skip absent optional layers, report absent required layers as violations
- [x] 3.3 Enrich violations with template metadata (`TemplateName`, `ContainerNamespace`) using `with` expressions in `CheckLayerContract`
- [x] 3.4 Update `ArchitectureDiagnosticFormatter` to include template/container fields in JSON output

## 4. Schema & Documentation

- [x] 4.1 Add `strict_layer_templates` and `audit_layer_templates` arrays with `layerTemplateContract` and `templateLayer` definitions to `dependencies.arch.schema.json`
- [x] 4.2 Update `docs/reference/yaml-schema.md` with template contract format and optional layer syntax
- [x] 4.3 Update `docs/contracts/index.md` with layer template contract examples

## 5. Tests

- [x] 5.1 Write `LayerTemplateExpanderTests.cs` — coverage for expansion, ID generation, metadata, empty templates, multiple containers
- [x] 5.2 Write `LayerTemplateContractTests.cs` — coverage for optional layers (present/absent), required missing layers, directional violations, cross-container isolation, coexistence with direct contracts
