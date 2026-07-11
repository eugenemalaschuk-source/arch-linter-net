## MODIFIED Requirements

### Requirement: Type and assembly attribute mapping requires no binary annotation dependency
`classification.attributes` and `classification.assembly_attributes` entries SHALL map by full attribute type name (`attribute: <FullTypeName>`) against attributes declared or referenced in scanned code, without requiring any ArchLinterNet-provided binary annotation assembly. Per issue #108's packaging decision, ArchLinterNet SHALL ship no binary or source-only annotation package in this wave — user-owned attributes mapped by full type name are the sole supported mechanism until a future, separately-decided change introduces an optional package.

#### Scenario: User-defined attribute is mappable by full type name
- **WHEN** a `classification.attributes` entry declares `attribute: Acme.Architecture.DomainLayerAttribute`
- **THEN** the model maps that attribute to the declared `role`/`metadata` regardless of which assembly defines `Acme.Architecture.DomainLayerAttribute`

#### Scenario: No ArchLinterNet annotation package is required or offered
- **WHEN** a project adopts `classification.attributes` mapping
- **THEN** it defines its own attribute type in its own codebase and does not add a reference to any ArchLinterNet-provided annotation package, since none is shipped in this wave
