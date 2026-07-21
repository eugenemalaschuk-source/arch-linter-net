## ADDED Requirements

### Requirement: Package dependency diagnostics render equivalent evidence in human, JSON, and SARIF output

Every `strict_package_dependency`/`audit_package_dependency` violation SHALL render the same source project,
forbidden-package-group display, and matched `PackageReference` evidence in human text output, unified JSON
output, and SARIF output. No adapter SHALL fall back to an empty or generic value for a field the underlying
violation carries.

#### Scenario: Human output shows package evidence
- **WHEN** a `strict_package_dependency` contract produces a violation for source `MyApp.Domain` against
  forbidden package group `forbidden_infra` matching `Microsoft.EntityFrameworkCore@8.0.0`
- **THEN** the human-formatted line identifies `MyApp.Domain` as the source and lists
  `Microsoft.EntityFrameworkCore@8.0.0` among the forbidden references

#### Scenario: Unified JSON shows package evidence
- **WHEN** the same violation is serialized to unified JSON
- **THEN** the JSON object's `source`, `forbidden_namespace`, and `forbidden_references` fields are non-empty
  and match the human-formatted evidence, and the object includes a `forbidden_package_group` field naming
  the matched package group

#### Scenario: SARIF, human, and JSON evidence are equivalent
- **WHEN** the same violation is rendered as human text, unified JSON, and SARIF
- **THEN** all three identify the same source project and the same matched package reference(s)
