## Why

Issue #172 requires a decision about built-in annotations. The catalog currently defers package design to #108 without explicitly recording the first-wave product decision, leaving a traceability gap.

## What Changes

- Record that the first semantic-role-catalog wave does not approve or ship built-in ArchLinterNet annotation types.
- Define catalog annotations as candidates and examples only.
- State that any optional annotation package or source-only distribution requires a separate decision and implementation in #108.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `semantic-role-catalog`: Record the first-wave built-in annotation decision and its handoff to #108.

## Impact

- Updates catalog documentation and specification only.
- No package, binary dependency, schema, runtime, or extraction changes.
