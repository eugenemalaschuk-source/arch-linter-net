## ADDED Requirements

### Requirement: v0.6.1 supports reusable assembly and discovered-project governance
The v0.6.1 compatibility surface SHALL let a consumer replace repeated directional assembly
contracts with one reviewed source-set contract and reuse solution-discovered production project
sets across project-metadata contracts, without weakening direct-reference or metadata enforcement.

#### Scenario: Large modular policy has no duplicated source inventory
- **WHEN** a consumer governs more than twenty module assemblies and multiple production-project
  metadata rules
- **THEN** one assembly set and reusable solution-derived project sets can express the invariants
  without copied per-module contracts or duplicated `analysis.projects` paths
