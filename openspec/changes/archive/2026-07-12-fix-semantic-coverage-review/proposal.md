## Why

Review of semantic-role coverage found public-schema drift and incomplete coverage semantics that can falsely classify types as governed or hide metadata extraction failures. These corrections are required before the feature can be merged safely.

## What Changes

- Align the public JSON Schema and authoring documentation with `scope: semantic_role` and semantic exclusions.
- Make semantic governance evaluate complete combined layer constraints for the concrete type.
- Include metadata extraction failures in deterministic semantic coverage evidence.
- Validate semantic roots and exclusions strictly, and add regression tests for all review findings.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-coverage-model`: constrain semantic roots and complete governance matching.
- `architecture-coverage-reporting`: include metadata extraction failures in semantic evidence.
- `yaml-contract-loading`: expose semantic coverage syntax in the public JSON Schema.

## Impact

Coverage validation/execution, JSON Schema, docs, and NUnit fixtures change. Existing non-semantic coverage behavior remains unchanged.
