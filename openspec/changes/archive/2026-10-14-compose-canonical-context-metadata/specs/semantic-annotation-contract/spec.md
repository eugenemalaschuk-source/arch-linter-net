## ADDED Requirements

### Requirement: Canonical context annotations enrich the existing primary role

The six manifest-defined context-only annotations SHALL contribute only their corresponding bounded metadata key and SHALL NOT assign or replace a primary role. A role's own effective metadata SHALL take precedence, followed by type-scope context values for missing keys, followed by assembly-scope context defaults for remaining missing keys. Equivalent values SHALL coalesce. Conflicting values at the same context scope SHALL omit only the affected context key and produce a deterministic diagnostic while preserving an otherwise valid role and unrelated metadata.

#### Scenario: Role metadata and separate context metadata compose without replacing the role
- **WHEN** a type has one canonical role annotation, a context-only annotation, and its assembly has a context default
- **THEN** the existing role remains primary, role metadata wins for keys it supplies, type context fills missing keys, and assembly context fills only the remaining missing keys

#### Scenario: Type context overrides an assembly context default
- **WHEN** a type and its assembly provide different context values for the same key and the winning role evidence does not provide that key
- **THEN** the type-scope value appears in the effective role metadata

#### Scenario: Context metadata alone does not classify a type
- **WHEN** a type or assembly carries canonical context annotations but no classification source assigns a role
- **THEN** no role descriptor is produced and no role is inferred from the context value

#### Scenario: Feature context does not become the Feature role
- **WHEN** a type carries `ArchLinterNet.Annotations.FeatureAttribute`
- **THEN** it contributes only the `feature` metadata key and does not receive the custom-mapping-only `Feature` role
