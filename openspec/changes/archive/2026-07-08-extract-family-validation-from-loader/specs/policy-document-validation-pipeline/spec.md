## ADDED Requirements

### Requirement: Family-specific policy validation is owned by dedicated validator classes
Each contract family's YAML-configuration validation SHALL be implemented by its own class implementing `IArchitecturePolicyDocumentValidator` (a single `Validate(ArchitectureContractDocument document)` method that throws on invalid configuration), rather than as a private method on `ArchitecturePolicyDocumentLoader`. `ArchitecturePolicyDocumentLoader` SHALL NOT contain family-specific validation logic in its own method bodies.

#### Scenario: Adding a validation rule to an existing family does not touch the loader
- **WHEN** a new validation rule is added to an existing family (e.g. a new required field check for `attribute_usage` contracts)
- **THEN** the change is made entirely within that family's validator class
- **AND** `ArchitecturePolicyDocumentLoader.cs` is not modified

#### Scenario: Coverage family validation is a single validator covering all scopes
- **WHEN** a policy document contains `coverage` contracts across the `namespace`, `rule_input`, `dependency_edge`, `project`, or `assembly` scopes
- **THEN** a single `CoverageValidator` class dispatches to the correct scope-specific validation, matching the dispatch behavior in place before this change

### Requirement: Validators execute in a fixed, documented order
`ArchitecturePolicyDocumentLoader.Load` SHALL invoke an ordered, internal pipeline of `IArchitecturePolicyDocumentValidator` instances after deserializing the document and assigning fallback contract ids. The pipeline order SHALL be fixed and SHALL reproduce the validation call order that existed immediately before this change was introduced, so that a document invalid in more than one respect fails with the same first-encountered exception as before.

#### Scenario: Duplicate-id validation still runs before family-specific validation
- **WHEN** a policy document has both a duplicate contract id and a separately-invalid `attribute_usage` contract
- **THEN** `Load` throws the duplicate-id `InvalidOperationException` (not the `attribute_usage` validation error), matching the order in place before this change

#### Scenario: Layer namespace validation still runs between acyclic-sibling and coverage validation
- **WHEN** a policy document has an invalid `acyclic_sibling` contract and separately invalid `layers`
- **THEN** `Load` throws the acyclic-sibling exception first, matching the relative order in place before this change

### Requirement: Validation messages and exception types are unchanged
Every extracted validator SHALL throw the same exception type with the same message text (including all interpolated values) as the corresponding validation logic produced before this change, for every currently-tested invalid-configuration scenario.

#### Scenario: Existing invalid-policy tests pass unchanged
- **WHEN** the existing per-family invalid-policy test suites (attribute usage, inheritance, composition, type placement, public API surface, package dependency/allow-only, assembly dependency/allow-only/independence, project metadata, interface implementation, coverage) are run against the extracted validators
- **THEN** every test SHALL pass without modification to its expected exception type or message assertion

### Requirement: The validation pipeline is local to the Contracts module
The `IArchitecturePolicyDocumentValidator` interface, all family-specific validator implementations, and the ordered pipeline collection SHALL reside within `ArchLinterNet.Core.Contracts` and SHALL NOT reference `ArchLinterNet.Core.Execution` or any other module that `Contracts` is not already permitted to depend on.

#### Scenario: Validator pipeline does not invoke ArchitectureContractFamilyDescriptor.AdditionalValidation
- **WHEN** `ArchitecturePolicyDocumentLoader.Load` runs its validator pipeline
- **THEN** no `ArchitectureContractFamilyDescriptor.AdditionalValidation` delegate is read or invoked
- **AND** the pipeline's validator list is defined entirely within `Contracts`, independent of `ArchitectureContractFamilyRegistry`
