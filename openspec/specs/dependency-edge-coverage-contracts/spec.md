# dependency-edge-coverage-contracts Specification

## Purpose
TBD - created by archiving change dependency-edge-coverage-contracts. Update Purpose after archive.
## Requirements
### Requirement: Dependency-edge coverage contracts classify observed first-party edges per declared layer pair
The system SHALL execute `strict_coverage` and `audit_coverage` contracts with `scope: dependency_edge` by classifying, for each declared pair in `between`, every observed first-party dependency edge (from `ArchitectureCoverageInventory.DependencyEdges`) whose source namespace matches the pair's source layer and whose target namespace matches the pair's target layer, as `covered`, `excluded`, or `uncovered`.

#### Scenario: An edge matching a declared pair with no governing contract is uncovered
- **GIVEN** a `scope: dependency_edge` coverage contract declaring `between: [[LayerA, LayerB]]`
- **AND** an observed first-party edge whose source namespace matches `LayerA` and target namespace matches `LayerB`
- **AND** no dependency, layer, independence, allow-only, protected, or expanded layer-template contract governs the pair `(LayerA, LayerB)`
- **AND** the edge matches no `exclude` entry
- **WHEN** validation runs for that contract's mode
- **THEN** the system reports that edge as an `"uncovered dependency edge"` finding identifying the source namespace, target namespace, and a representative source type

#### Scenario: A layer pair not declared in any between list is not evaluated
- **GIVEN** a `scope: dependency_edge` coverage contract declaring `between: [[LayerA, LayerB]]`
- **AND** an observed first-party edge between `LayerC` and `LayerD`, a pair not declared in any `between` list
- **WHEN** validation runs for that contract
- **THEN** the edge produces no coverage finding and is not counted in that contract's coverage summary

### Requirement: A dependency contract governs the directed layer pair it declares
Dependency-edge coverage classification SHALL treat a declared pair `(A, B)` as governed when an `ArchitectureDependencyContract` exists with `Source` equal to `A` and `Forbidden` containing `B`.

#### Scenario: An edge governed by a dependency contract's forbidden list is covered
- **GIVEN** a `scope: dependency_edge` coverage contract declaring `between: [[LayerA, LayerB]]`
- **AND** an `ArchitectureDependencyContract` with `source: LayerA` and `forbidden: [LayerB]`
- **AND** an observed first-party edge whose source namespace matches `LayerA` and target namespace matches `LayerB`
- **WHEN** dependency-edge coverage classification runs
- **THEN** the edge is classified `covered`

### Requirement: A layer contract governs every pair of layers in its declared chain
Dependency-edge coverage classification SHALL treat a declared pair `(A, B)` as governed, in either direction, when an `ArchitectureLayerContract` exists whose `layers` list contains both `A` and `B`.

#### Scenario: An edge governed by a layer contract's chain is covered
- **GIVEN** a `scope: dependency_edge` coverage contract declaring `between: [[LayerA, LayerB]]`
- **AND** an `ArchitectureLayerContract` with `layers: [LayerA, LayerB, LayerC]`
- **AND** an observed first-party edge whose source namespace matches `LayerA` and target namespace matches `LayerB`
- **WHEN** dependency-edge coverage classification runs
- **THEN** the edge is classified `covered`

### Requirement: An independence contract governs every pair of layers it declares, bidirectionally
Dependency-edge coverage classification SHALL treat a declared pair `(A, B)` as governed, in either direction, when an `ArchitectureIndependenceContract` exists whose `layers` list contains both `A` and `B`.

#### Scenario: An edge governed by an independence contract is covered
- **GIVEN** a `scope: dependency_edge` coverage contract declaring `between: [[LayerA, LayerB]]`
- **AND** an `ArchitectureIndependenceContract` with `layers: [LayerA, LayerB]`
- **AND** an observed first-party edge whose source namespace matches `LayerA` and target namespace matches `LayerB`
- **WHEN** dependency-edge coverage classification runs
- **THEN** the edge is classified `covered`

### Requirement: An expanded layer template governs a pair when its container layers match both declared layers' namespaces
Dependency-edge coverage classification SHALL treat a declared pair `(A, B)` as governed when an expanded `strict_layer_templates`/`audit_layer_templates` contract exists whose expanded layer namespaces include at least one namespace matching declared layer `A`'s pattern and at least one namespace matching declared layer `B`'s pattern.

#### Scenario: An edge governed by an expanded layer template is covered
- **GIVEN** a `scope: dependency_edge` coverage contract declaring `between: [[LayerA, LayerB]]`
- **AND** a layer template whose expansion produces layers matching declared layer `A`'s and declared layer `B`'s namespace patterns
- **AND** an observed first-party edge whose source namespace matches `LayerA` and target namespace matches `LayerB`
- **WHEN** dependency-edge coverage classification runs
- **THEN** the edge is classified `covered`

### Requirement: An allow-only contract governs the entire outbound surface of its source layer
Dependency-edge coverage classification SHALL treat a declared pair `(A, B)` as governed when an `ArchitectureAllowOnlyContract` exists with `Source` equal to `A`, regardless of whether `B` appears in that contract's `allowed` list — an allow-only contract governs every reference out of its source layer, not only the explicitly allowed targets.

#### Scenario: An edge governed by an allow-only contract is covered even when the target is not in the allowed list
- **GIVEN** a `scope: dependency_edge` coverage contract declaring `between: [[LayerA, LayerB]]`
- **AND** an `ArchitectureAllowOnlyContract` with `source: LayerA` whose `allowed` list does not contain `LayerB`
- **AND** an observed first-party edge whose source namespace matches `LayerA` and target namespace matches `LayerB`
- **WHEN** dependency-edge coverage classification runs
- **THEN** the edge is classified `covered`

### Requirement: A protected contract governs every reference into its protected layer
Dependency-edge coverage classification SHALL treat a declared pair `(A, B)` as governed when an `ArchitectureProtectedContract` exists whose `protected` list contains `B`, regardless of whether `A` appears in that contract's `allowed_importers` list — a protected contract governs every reference into its protected layer, both allowed and disallowed importers.

#### Scenario: An edge governed by a protected contract is covered even when the source is not an allowed importer
- **GIVEN** a `scope: dependency_edge` coverage contract declaring `between: [[LayerA, LayerB]]`
- **AND** an `ArchitectureProtectedContract` with `protected: [LayerB]` whose `allowed_importers` list does not contain `LayerA`
- **AND** an observed first-party edge whose source namespace matches `LayerA` and target namespace matches `LayerB`
- **WHEN** dependency-edge coverage classification runs
- **THEN** the edge is classified `covered`

### Requirement: Dependency-edge coverage exclusions require a reason and match by declared pair
Dependency-edge coverage contracts SHALL honor `exclude` entries that set `between` to a declared layer pair together with a non-empty `reason`, suppressing every observed edge matching that pair.

#### Scenario: An excluded pair suppresses its edges
- **GIVEN** a `scope: dependency_edge` coverage contract declaring `between: [[LayerA, LayerB]]`
- **AND** an `exclude` entry with `between: [LayerA, LayerB]` and a documented reason
- **AND** the pair `(LayerA, LayerB)` is not governed by any dependency, layer, independence, allow-only, protected, or expanded layer-template contract
- **WHEN** dependency-edge coverage classification runs
- **THEN** observed edges matching `(LayerA, LayerB)` are classified `excluded`, not `uncovered`

### Requirement: Dependency-edge coverage exclusions reject other scopes' matcher fields and undeclared pairs
The loader SHALL reject a `dependency_edge`-scope `exclude` entry that declares `namespace`, `namespace_suffix`, `project`, `assembly`, or `contract_id` — those matchers belong to other coverage scopes, and a dependency-edge exclusion always suppresses the entire declared pair regardless of any other field, so declaring them would misleadingly suggest a narrower exclusion than what actually happens. The loader SHALL also reject an `exclude` entry whose `between` pair is not declared in that same contract's own `between` list.

#### Scenario: An exclusion using a namespace matcher is rejected
- **WHEN** a `scope: dependency_edge` coverage contract's `exclude` entry declares `namespace`, `namespace_suffix`, `project`, `assembly`, or `contract_id` in addition to `between`
- **THEN** the loader rejects the document as invalid

#### Scenario: An exclusion for an undeclared pair is rejected
- **WHEN** a `scope: dependency_edge` coverage contract's `exclude` entry declares a `between` pair that does not appear in that contract's own `between` list
- **THEN** the loader rejects the document as invalid

### Requirement: Dependency-edge coverage findings and summary carry source/target evidence
Dependency-edge coverage findings and the `coverage_summary` entries for `scope: dependency_edge` SHALL include the observed edge's source namespace, target namespace, and a representative source type for `uncovered` findings.

#### Scenario: Uncovered edge evidence includes source and target namespaces
- **WHEN** an observed first-party edge is reported as uncovered
- **THEN** the finding/summary evidence includes the edge's source namespace, target namespace, and a representative source type from the source namespace

### Requirement: Dependency-edge coverage scope rejects fields belonging to other scopes
The loader SHALL require `scope: dependency_edge` coverage contracts to declare a non-empty `between` list of two-element declared-layer-name pairs and SHALL reject `roots` or `contract_ids` declared on a `dependency_edge`-scope contract.

#### Scenario: A dependency_edge contract without between is rejected
- **WHEN** a coverage contract declares `scope: dependency_edge` and omits `between` or declares an empty `between` list
- **THEN** the loader rejects the document as invalid

#### Scenario: A dependency_edge contract referencing an undeclared layer is rejected
- **WHEN** a `scope: dependency_edge` coverage contract's `between` list references a layer name not present in the document's declared `layers`
- **THEN** the loader rejects the document as invalid

