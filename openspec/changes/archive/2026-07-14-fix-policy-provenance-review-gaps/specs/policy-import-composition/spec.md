## ADDED Requirements

### Requirement: Catalog-expanded templates retain their source owner
The system SHALL bind provenance to the exact layer-template contract instances
materialized by the contract catalog. Runtime and consistency diagnostics for
imported strict and audit templates SHALL identify their source template.

#### Scenario: Catalog re-expands an imported template
- **WHEN** catalog construction expands an imported layer template after policy
  provenance binding
- **THEN** a violation from the catalog instance retains the fragment template
  location
