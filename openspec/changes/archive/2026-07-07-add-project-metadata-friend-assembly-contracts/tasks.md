## 1. Discovery and contract model

- [x] 1.1 Extend project discovery models and parser coverage to expose selected properties, friend assemblies, and project references, including nearest `Directory.Build.props` inheritance needed for repo-level metadata.
- [x] 1.2 Add policy contract models, loader validation, and contract catalog wiring for `strict_project_metadata` and `audit_project_metadata`.

## 2. Execution and diagnostics

- [x] 2.1 Implement project metadata contract evaluation for required properties, forbidden property values, friend-assembly allowlists, and forbidden project references.
- [x] 2.2 Emit deterministic diagnostics and configuration checks for undiscoverable project targets or unusable contract definitions.

## 3. Schema, docs, and validation

- [x] 3.1 Update JSON schema, policy-format reference, contract docs, examples, and AI-facing guidance for project metadata contracts.
- [x] 3.2 Add NUnit coverage for discovery parsing, strict and audit contract behavior, and project-reference leakage scenarios; then run formatting and repository acceptance validation.
