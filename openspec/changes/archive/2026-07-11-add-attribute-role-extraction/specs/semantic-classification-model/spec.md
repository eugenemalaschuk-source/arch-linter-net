## MODIFIED Requirements

### Requirement: No runtime behavior is introduced by this design
This change SHALL NOT add any C# binding or extraction/role-assignment logic for the `inheritance`, `namespace`, or `path` classification sources, for `classification.overrides`/`classification.exclusions`, or for `layers.<name>.selector` matching/consumption, and SHALL NOT add any load-time guard rejecting policies that declare these constructs. A policy declaring any of these constructs before their bindings exist SHALL be schema-valid but produce no behavior for them. This requirement no longer applies to the `type_attribute` and `assembly_attribute` classification sources (`classification.attributes` and `classification.assembly_attributes`), which the `attribute-role-extraction` capability makes fully functional.

#### Scenario: Declaring unimplemented classification constructs does not throw
- **WHEN** a policy declares `classification.overrides`, `classification.exclusions`, `classification.inheritance`, `classification.namespace`, `classification.path`, or a `layers.<name>.selector` field before their implementation lands
- **THEN** policy loading and validation SHALL proceed exactly as if the construct were absent, with no exception thrown and no role ever assigned from it

#### Scenario: Declaring classification.attributes or classification.assembly_attributes now produces role/metadata assignments
- **WHEN** a policy declares `classification.attributes` or `classification.assembly_attributes` entries matching attributes present in scanned code
- **THEN** the extraction engine assigns role/metadata per the `attribute-role-extraction` capability, rather than treating the declaration as an inert no-op
