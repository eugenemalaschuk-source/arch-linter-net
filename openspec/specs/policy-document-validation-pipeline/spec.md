# policy-document-validation-pipeline Specification

## Purpose
Owns how a loaded policy document is validated: an ordered raw-YAML validation pipeline that runs
before deserialization, an ordered document-validator pipeline that runs after it, and the boundary
that keeps both out of `ArchitecturePolicyDocumentLoader`, which only orders the load stages.
## Requirements
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

### Requirement: Static policy validation pass
The policy document validation pipeline SHALL execute each assembly-independent root schema, import, composition, contract cross-reference, selector syntax/binding, declaration, path-format, baseline-reference, and API-snapshot-reference validation once for policy check.

#### Scenario: Duplicate contract ID
- **WHEN** root and imported fragments compose duplicate contract IDs
- **THEN** the pipeline reports one typed configuration failure with provenance

#### Scenario: Import cycle or repository escape
- **WHEN** an import creates a cycle or escapes the repository boundary
- **THEN** the pipeline rejects the policy before any fact-dependent validation is attempted

### Requirement: Raw policy YAML validation is owned by dedicated raw validator classes
Validation that inspects the effective policy YAML node tree before deserialization SHALL be
implemented by classes implementing an internal raw-document validator interface (a single
`Validate(ArchitecturePolicyRawDocument document)` method that throws on invalid raw configuration),
rather than as methods on `ArchitecturePolicyDocumentLoader`. `ArchitecturePolicyDocumentLoader`
SHALL NOT reference any YamlDotNet representation-model type anywhere in its own source - neither in a
member signature nor inside a method body, lambda, or local declaration - so that a raw-node algorithm
whose signature exposes no node type cannot be reintroduced onto it.

#### Scenario: Adding a raw node rule to an existing capability does not touch the loader
- **WHEN** a new raw YAML node rule is added for an existing capability (for example a new
  known-key check on layout-convention matchers)
- **THEN** the change is made entirely within that capability's raw validator class
- **AND** `ArchitecturePolicyDocumentLoader` is not modified

#### Scenario: A raw node algorithm hidden in a loader method body is rejected
- **WHEN** a method that takes only a YAML string and builds the node tree inside its own body is
  added to `ArchitecturePolicyDocumentLoader` - the shape the extracted raw checks had before this
  change
- **THEN** the loader boundary guard fails, on both the loader's source and its compiled members,
  locals and captured state

#### Scenario: Every raw validator is reachable from the pipeline
- **WHEN** the Core assembly is inspected for implementations of the raw-document validator interface
- **THEN** every concrete implementation appears exactly once in the raw validation pipeline

### Requirement: Raw validators execute in a fixed, documented order
The policy document loader SHALL invoke an ordered, internal pipeline of raw-document validators
after import composition and effective-schema validation and before deserialization. The pipeline
order SHALL be fixed and SHALL reproduce the raw validation call order that existed immediately
before this change, so that a policy invalid in more than one raw respect fails with the same
first-encountered exception as before.

#### Scenario: Layer raw validation still runs before contextual raw validation
- **WHEN** a policy declares both an unknown layer property and an unknown contextual-selector
  property
- **THEN** loading fails with the layer diagnostic, matching the order in place before this change

#### Scenario: `when` placement validation still runs last among raw validators
- **WHEN** a policy declares both an unknown layer-template property and a `when` field at an
  unapproved location
- **THEN** loading fails with the layer-template diagnostic, matching the order in place before this
  change

### Requirement: Raw validation preserves diagnostics and provenance evidence
Every extracted raw validator SHALL throw the same exception type with the same message text
(including all interpolated values) as the corresponding loader logic produced before this change,
and SHALL make the same provenance validation-subject transitions, so that reported authored and
imported source locations are unchanged. The validation subject SHALL be reset once by the loader
after the whole raw stage, not by individual raw validators.

#### Scenario: Malformed root policy reports the same location
- **WHEN** a monolithic policy declares an unknown property on a layer
- **THEN** the reported failure category, message and source location are identical to those produced
  before this change

#### Scenario: Malformed imported fragment reports the fragment location
- **WHEN** an imported fragment declares a raw node shape that the composed-policy schema accepts and
  raw validation rejects
- **THEN** the failure is enriched with that fragment's authored location, identical to the location
  produced before this change

### Requirement: The policy document loader orders load stages without owning capability-specific algorithms
`ArchitecturePolicyDocumentLoader` SHALL sequence root resolution, import resolution and composition,
effective-schema validation, raw YAML validation, deserialization, fallback-ID assignment, provenance
binding, deferred classification-path detection, reviewed API snapshot resolution, source-set
expansion and the document-validator pipeline as explicit, deterministically ordered stages. It SHALL
NOT implement any stage that is specific to a contract family or other policy capability; each such
stage SHALL be delegated to a dedicated type. Stages that are not capability-specific - resolving and
reading the selected root, delegating to the import resolution and composition components, and
configuring the YAML deserializer - MAY remain on the loader. Cancellation checks and exception
enrichment SHALL remain deterministic and unchanged.

#### Scenario: Stage order is unchanged
- **WHEN** a policy that exercises imports, source sets, reviewed API snapshots and document
  validation is loaded
- **THEN** stages run in the same relative order as before this change, and the resulting document is
  identical

#### Scenario: Raw validation still precedes deserialization
- **WHEN** a policy contains a raw node shape that deserialization would silently discard
- **THEN** loading fails during raw validation, before any model object is constructed

