## Why

Project and assembly metadata already define important architectural boundaries in this repo, but ArchLinterNet currently governs only code references, public API surface, and declared package references. We need first-class policy support for `.csproj` properties, friend assemblies, and project-reference leakage so architecture validation can catch packaging and boundary drift before release.

## What Changes

- Add a new contract family for validating selected project metadata, friend assembly declarations, and project-reference leakage from discovered projects.
- Extend project discovery so each discovered project exposes selected MSBuild properties, `InternalsVisibleTo` entries, and declared `ProjectReference` targets alongside existing package metadata.
- Support strict and audit variants with deterministic diagnostics that identify the project, metadata key or friend assembly, expected rule, actual value, and source file when available.
- Update the YAML schema, policy authoring docs, examples, and AI-facing guidance for the new contract family.

## Capabilities

### New Capabilities
- `project-metadata-contracts`: Validate selected project metadata properties, allowed friend assemblies, and production-to-test project-reference leakage for discovered projects.

### Modified Capabilities
- `project-discovery`: Extend discovered project metadata to include selected MSBuild properties, friend assemblies, and project references needed by metadata governance contracts.

## Impact

- Affected code: project discovery models/parsers, policy models and loader, contract execution, diagnostics/reporting, JSON schema, docs, examples, and NUnit coverage.
- Affected inputs: policies can declare new strict/audit project metadata contracts; no existing contract family behavior should change.
