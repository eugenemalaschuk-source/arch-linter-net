## ADDED Requirements

### Requirement: Canonical identity and compatibility diagnostics are actionable and consistent

When canonical role annotation evidence is present, the annotation-aware tool SHALL require exactly one valid assembly catalog identity and SHALL resolve the evidence only when its catalog generation and package SemVer are supported. Missing, malformed, duplicated, or unsupported identity markers and unknown FQNs under the reserved namespace SHALL produce deterministic diagnostics that identify the subject, exact FQN, and compatibility or identity reason. Any policy mapping whose attribute FQN is in the reserved namespace SHALL produce a deterministic configuration diagnostic, even when it repeats the canonical role or metadata exactly, and SHALL NOT reinterpret that identity. Analysis diagnostics SHALL be represented consistently in human, JSON, SARIF, and Testing outputs.

#### Scenario: Canonical evidence without a valid catalog marker fails closed
- **WHEN** a target contains a known canonical role FQN but its assembly has no unique valid marker, an unsupported generation, or a malformed or unsupported package version
- **THEN** the tool reports the applicable actionable diagnostic and does not resolve that annotation as role evidence

#### Scenario: Unknown reserved FQN fails closed
- **WHEN** a target contains an attribute FQN under `ArchLinterNet.Annotations` that is not a supported canonical role identity
- **THEN** the tool reports the exact unsupported FQN and does not guess a role by simple name, namespace prefix, or inheritance

#### Scenario: YAML cannot name a reserved canonical identity
- **WHEN** policy YAML maps an attribute FQN in the reserved namespace, including an exact redundant mapping of a known canonical role FQN
- **THEN** policy validation reports the reserved-identity configuration error and does not reinterpret the annotation

#### Scenario: Analysis diagnostic projections agree
- **WHEN** one of these identity or compatibility conditions is encountered during analysis
- **THEN** human, JSON, SARIF, and Testing surfaces preserve the same subject, exact FQN, reason, and deterministic ordering

### Requirement: Tool capability and schema help expose the supported canonical role catalog

The machine-readable capability inventory SHALL advertise the canonical role FQNs recognized by the tool, each corresponding role and allowed scope, the reserved namespace, supported catalog generations, supported package SemVer ranges, and minimum annotation-aware tool version. Packaged policy-schema help SHALL explain that canonical roles are exact-FQN semantic evidence requiring a supported catalog identity and do not grant policy authority. Capability and schema catalog projections SHALL be validated against the versioned canonical manifest so drift is reported by the repository checks.

#### Scenario: Capability inventory describes the exact supported role identities
- **WHEN** tooling reads the capability inventory for an annotation-aware release
- **THEN** it can determine the exact canonical role FQNs and their type or assembly scope together with the supported catalog and package versions

#### Scenario: Schema help explains the canonical annotation boundary
- **WHEN** an adopter inspects the packaged policy schema or semantic-classification help
- **THEN** the metadata explains exact identity matching, version compatibility, and that annotations do not create policy rules or reviewed API membership

#### Scenario: Catalog projection drift is detected
- **WHEN** a supported role FQN, role name, scope, generation, or package range differs between a capability/schema projection and the canonical manifest
- **THEN** the focused repository validation fails and identifies the mismatched projection
