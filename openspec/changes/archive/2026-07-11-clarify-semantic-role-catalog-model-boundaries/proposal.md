## Why

PR #284 review found that the catalog's metadata-only assembly attribute example is not representable by the approved semantic-classification model: every mapping entry requires a role and classification does not merge metadata across sources. The catalog also needs an explicit single-role rule so readers do not interpret related roles as simultaneously assigned tags.

## What Changes

- Replace the invalid metadata-only assembly mapping with a role-bearing assembly classification example.
- Explicitly defer metadata-only assembly context such as `[assembly: BoundedContext("Billing")]` until a separate model/schema change defines metadata merge semantics.
- Document that one classification result has one role and its winning source's metadata; related catalog roles are alternatives, not accumulated tags.
- Mark `asmdef` and package-reference evidence as future guidance rather than current classification sources.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `semantic-role-catalog`: Clarify assembly-level examples, single-role semantics, and the boundary between current classification sources and future evidence.

## Impact

- Updates the semantic role catalog and its OpenSpec requirement.
- No runtime, schema, extraction, annotation package, selector, or dependency changes.
