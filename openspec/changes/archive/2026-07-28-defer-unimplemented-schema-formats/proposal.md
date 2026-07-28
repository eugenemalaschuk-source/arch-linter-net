## Why

The initial packaged registry incorrectly advertised placeholder schemas for finding, cache, and profile formats whose owning implementation slices are unfinished. Immutable release contracts must describe real persisted formats only.

## What Changes

- Limit the shipped registry to implemented policy, baseline, API snapshot, and build-receipt formats.
- Describe the line-oriented API snapshot and actual BuildReceiptV1 serialization accurately.
- Defer finding, cache, and profile schemas until their owning slices ship verified output.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `packaged-schema-registry`: Publish only implemented, validated persisted contracts.
- `adoption-stabilization-compatibility`: Distinguish defined future envelopes from formats currently shipped by the package.

## Impact

Registry resources, package validation, public documentation, and compatibility claims.
