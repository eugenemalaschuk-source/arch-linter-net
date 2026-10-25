## ADDED Requirements

### Requirement: Stable v0.10 candidates prove semantic annotations from the packed candidate

Whenever a Checkpoint B candidate manifest contains `ArchLinterNet.Annotations`, Checkpoint B SHALL restore and exercise that package together with the manifest-bound CLI, Core, Testing, schema, and capability artifacts in external synthetic consumers. The annotation consumer scenario SHALL run on every required platform, SHALL use no ArchLinterNet `ProjectReference`, and SHALL be required by platform merge and final evidence aggregation. The scenario outcome and candidate package identities SHALL be bound to the candidate manifest digest. Candidate manifests without the annotations package SHALL retain their existing required scenario inventory.

The external-consumer proof SHALL cover plain class-library, modular/layered, DDD, CQRS/API, infrastructure/adapter, public reusable library, and supported Unity-style assembly shapes; custom-only consumers without the annotation package; and consumers that combine canonical and user-owned custom attributes. It SHALL verify zero annotation runtime dependency, no injected annotation types in reviewed public API membership by default, canonical role recognition without built-in mappings, custom mapping parity, role conflict determinism, bounded context metadata composition, #525 membership orthogonality, unchanged policy authority, and fail-safe package/tool compatibility diagnostics. Selector-backed layers, contextual contracts, semantic coverage, and explain SHALL consume the existing role index. Human, JSON, SARIF, Testing, explain, and coverage projections SHALL agree on the same semantic facts and diagnostics.

#### Scenario: Stable v0.10 candidate requires the annotation consumer evidence

- **WHEN** the immutable candidate manifest contains the five-package stable v0.10 inventory, including the package-only `ArchLinterNet.Annotations` subject
- **THEN** each required Checkpoint B platform executes the external semantic-annotation consumer scenario against the same manifest-bound candidate feed and records its outcome with the candidate manifest digest
- **AND** platform merge and final evidence aggregation fail if the scenario is missing, duplicated, failed, or bound to another candidate

#### Scenario: Legacy and preview candidates preserve their existing inventory

- **WHEN** the immutable candidate manifest uses the existing four-package v2 inventory without `ArchLinterNet.Annotations`
- **THEN** Checkpoint B validates its four package and symbol subjects and applies its existing required scenario inventory without requiring annotation evidence

#### Scenario: Packed consumers preserve annotation and policy boundaries

- **WHEN** the external consumer matrix restores the candidate annotation package and analyzes canonical-only, custom-only, and mixed synthetic projects with the candidate CLI and Testing API
- **THEN** the package contributes source without a runtime annotation assembly or default reviewed API membership, canonical and equivalent custom mappings produce the expected semantic facts through the shared role index, and conflicts, metadata composition, and version skew are deterministic and actionable
- **AND** adding annotations does not grant dependency permissions or alter baselines, ignores, topology, strict/audit outcomes, or selector authority
- **AND** Human, JSON, SARIF, Testing, explain, and coverage projections agree on the semantic facts and diagnostics
