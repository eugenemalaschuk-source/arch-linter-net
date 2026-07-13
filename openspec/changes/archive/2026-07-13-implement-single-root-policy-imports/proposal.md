## Why

Large architecture policies currently have to remain in one YAML file even though the approved import format and composition semantics are already specified. Issue #281 makes that design executable while preserving the existing one-root loader API and monolithic-policy behavior.

## What Changes

- Add deterministic, bounded resolution of explicit local imports relative to each declaring document.
- Parse the selected entry document as the sole root and every reached document as a fragment, independent of filenames.
- Compose keyed definitions, ordered collections, singleton settings, classification data, and registered contract families according to the approved conflict rules.
- Validate root, fragment, and composed effective document shapes and publish explicit root and fragment schemas.
- Expose stable loading error categories for graph, path, shape, and composition failures.
- Preserve existing behavior and public entry points for policies without imports.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `policy-import-composition`: Add stable policy-loading error categories to the already-approved runtime import and composition requirements.

## Impact

The change is contained in `ArchLinterNet.Core` policy loading and IO seams, the published policy schemas, and Core NUnit tests. CLI and Testing continue to supply one root path and consume the same resolved `ArchitectureContractDocument`. Core adds JsonSchema.Net, already used by the test project, to validate composed effective policies against the embedded published schema; no adapter API changes.
