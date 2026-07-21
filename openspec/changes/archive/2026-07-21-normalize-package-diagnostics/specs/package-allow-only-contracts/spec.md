## ADDED Requirements

### Requirement: Package allow-only diagnostics use a distinct typed diagnostic kind

`package_allow_only` violations SHALL be represented by a `PackageAllowOnlyDiagnostic` with
`Kind == ArchitectureDiagnosticKind.PackageAllowOnly`, distinct from the generic `DependencyDiagnostic` used
for layer/namespace-style contracts and from `PackageDependencyDiagnostic` used for deny-list package
contracts.

#### Scenario: Allow-only violation has its own diagnostic kind
- **WHEN** a `strict_package_allow_only`/`audit_package_allow_only` contract produces a violation
- **THEN** the resulting diagnostic's `Kind` is `ArchitectureDiagnosticKind.PackageAllowOnly`

#### Scenario: Allow-only diagnostic reports package logical-location kind in SARIF
- **WHEN** a `package_allow_only` violation is rendered as a SARIF result
- **THEN** the result's logical location `kind` is `"package"`, not `"namespace"` or `"type"`

### Requirement: Package allow-only diagnostics render equivalent evidence in human, JSON, and SARIF output

Every `package_allow_only` violation SHALL render the same source project and disallowed-package evidence in
human text, unified JSON, and SARIF output, with no adapter falling back to an empty or generic value for a
field the underlying violation carries.

#### Scenario: Unified JSON shows allowed package groups
- **WHEN** a `package_allow_only` violation is serialized to unified JSON
- **THEN** the JSON object includes an `allowed_package_groups` field listing the contract's configured
  `allowed` package group names, alongside non-empty `source` and `forbidden_references` fields
