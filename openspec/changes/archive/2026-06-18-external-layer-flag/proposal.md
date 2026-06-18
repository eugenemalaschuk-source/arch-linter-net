## Why

When a policy declares layers for external SDKs, engine types, or first-party MonoBehaviours whose assemblies aren't available in the scanning environment, the linter reports `[] -> empty layer namespace` and exits with code 1 before running any contracts. These layers are legitimate — they serve as forbidden-reference targets in dependency contracts. Today the only workaround is wrapping the validator in C# to filter violations, which blocks CLI-only usage entirely.

## What Changes

- Add an `external: true` flag to the layer schema
- Suppress the `"empty layer namespace"` configuration diagnostic for layers marked `external: true`
- No change to dependency scanning or contract checking — namespace string matching works regardless of whether target-side types are loaded
- If types ARE found for an external layer (SDK present in search paths), the linter uses them normally
- Update the JSON Schema to include the new `external` property

## Capabilities

### New Capabilities

- `external-layer-support`: Add `external: true` boolean flag to layer declarations, suppressing configuration empty-layer diagnostics for intentionally unloadable namespaces while preserving full contract-checking behavior

### Modified Capabilities

<!-- No existing specs are being modified. -->

## Impact

- **ArchitectureLayer model** (`ArchitectureContractModels.cs`): new `External` boolean property
- **Configuration check** (`ArchitectureContractRunner.cs`): skip empty-layer diagnostic when `External == true`
- **JSON schema** (`schema/dependencies.arch.schema.json`): add `external` to layer definition
- **Tests** (`ConfigurationCheckTests.cs`): add test cases for external layer behavior
- **Non-breaking**: existing policies without `external: true` continue to work identically
