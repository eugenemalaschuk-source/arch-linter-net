## ADDED Requirements

### Requirement: A shared coverage inventory is built from existing discovery and resolution facts
The system SHALL provide an `ArchitectureCoverageInventory` that aggregates first-party namespaces, representative types, declared layers, expanded layer templates, project/assembly facts, and dependency edges by reusing `ArchitectureTypeIndex`, `ArchitectureLayerResolver`, `NamespaceGlobPattern`, `LayerTemplateExpander`, `ArchitectureProjectDiscovery`, and `ArchitectureReferenceGraph`, without re-implementing namespace matching, layer resolution, template expansion, or type/reference scanning.

#### Scenario: Inventory reuses the session's already-built type index and reference graph
- **WHEN** the coverage inventory is built for an analysis session
- **THEN** it reads types and references from the session's existing `ArchitectureTypeIndex` and `ArchitectureReferenceGraph` instead of performing a separate reflection or IL scan

### Requirement: Inventory namespace and representative-type collection is deterministic
The inventory SHALL list first-party namespaces sorted with ordinal string comparison and SHALL associate each namespace with exactly one representative type chosen by a deterministic rule (the alphabetically-first type's full name within that namespace).

#### Scenario: Repeated builds produce identical namespace ordering
- **WHEN** the inventory is built twice from the same target assemblies without any code change
- **THEN** both builds produce namespace lists in the same order and the same representative type per namespace

### Requirement: Inventory dependency edges are namespace-level, deduplicated, and sorted
The inventory SHALL derive dependency edges between first-party namespaces from the session's reference graph, excluding self-edges, deduplicating edges with the same source and target, and sorting edges first by source namespace then by target namespace using ordinal string comparison.

#### Scenario: Duplicate type-level references collapse into one namespace edge
- **WHEN** multiple types in namespace A reference multiple types in namespace B
- **THEN** the inventory reports exactly one dependency edge from A to B

#### Scenario: A type referencing another type in its own namespace produces no edge
- **WHEN** a type's only references are to other types within the same namespace
- **THEN** the inventory reports no dependency edge for that namespace

### Requirement: Inventory preserves layer template exhaustiveness without re-deriving it
The inventory SHALL include the expanded layer templates produced by `LayerTemplateExpander.Expand()` exactly as returned, including each expansion's `Exhaustive` flag, without recomputing or altering exhaustiveness.

#### Scenario: An exhaustive template's expansion is available to the inventory
- **WHEN** a document declares a layer template with `exhaustive: true`
- **THEN** the inventory's expanded-templates collection includes that template's expansion with `Exhaustive` set to `true`

### Requirement: Inventory project and assembly facts are absent rather than fabricated when discovery is unavailable
The inventory SHALL expose project/assembly facts (source roots, assembly names, search paths) sourced verbatim from a `ProjectDiscoveryResult` when one was resolved for the session, and SHALL leave those facts absent (not a fabricated empty or default value implying certainty) when no discovery result is available.

#### Scenario: Discovery result is available
- **WHEN** the analysis session has a resolved `ProjectDiscoveryResult`
- **THEN** the inventory's project/assembly facts match that result's source roots, assembly names, and search paths

#### Scenario: Discovery result is unavailable
- **WHEN** the analysis session has no resolved `ProjectDiscoveryResult`
- **THEN** the inventory exposes its project/assembly facts as absent, distinguishable from an empty discovered set

### Requirement: Inventory construction is opt-in and does not affect existing validation behavior
The inventory SHALL be built lazily, only when a consumer requests it, and SHALL NOT be constructed as part of existing contract execution paths that do not request it, so that policies without coverage contracts see no behavior or performance change.

#### Scenario: Validating a policy without coverage contracts never builds the inventory
- **WHEN** a policy declares no `strict_coverage`/`audit_coverage` contracts and validation runs
- **THEN** the coverage inventory is never constructed during that run

### Requirement: Inventory performs no coverage classification
The inventory SHALL NOT classify any namespace, project, assembly, or dependency edge as `covered`, `excluded`, `uncovered`, `unknown`, `stale`, or `empty-input`; it SHALL only expose the raw facts a future classifier needs.

#### Scenario: Inventory output contains no classification status
- **WHEN** the inventory is built for any document
- **THEN** none of its exposed collections carry a coverage status value
