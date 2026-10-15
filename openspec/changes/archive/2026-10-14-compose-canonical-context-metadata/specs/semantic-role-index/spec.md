## ADDED Requirements

### Requirement: Role descriptors expose composed canonical context metadata

The role index SHALL expose the effective role metadata produced by canonical context composition through its existing descriptor and enumeration APIs. Context metadata SHALL not create a descriptor when no source assigns a role. Existing selectors and contextual contracts SHALL consume the composed metadata through the same role descriptor model.

#### Scenario: Index exposes composed role and context metadata
- **WHEN** a role is enriched by type- or assembly-scope canonical context annotations
- **THEN** the existing lookup and enumeration APIs return the role with the effective composed metadata

#### Scenario: Context-only evidence produces no role descriptor
- **WHEN** canonical context annotations are present but no source assigns a role
- **THEN** role lookup reports no descriptor for the type

#### Scenario: Selectors and contextual contracts use the composed descriptor
- **WHEN** a selector or contextual contract evaluates a type whose role metadata includes canonical context evidence
- **THEN** it observes the same role and metadata as the role index without requiring a separate annotation-specific consumer
