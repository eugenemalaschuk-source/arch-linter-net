## Why

Review found that the immutable registry was publishing stale source-schema descriptions and overstating `analysis-profile` read support. The published package and its owning specifications must accurately describe the actual writer-only profile contract before the release registry can be trusted.

## What Changes

- Remove deferred-registration wording from the shipped cache and profile schema descriptions and refresh their manifest digests.
- Declare `analysis-profile` as write-only until a public reader is implemented, with explicit per-format support assertions.
- Synchronize the owning profile capability with its packaged-registry enrollment.
- Correct the prior archived change record: only cache and profile were newly enrolled; normalized-finding was retained and verified.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `analysis-profile`: Describe packaged registration and writer-only support accurately.
- `packaged-schema-registry`: Require metadata to reflect each format's actual read/write support.
- `adoption-stabilization-compatibility`: Clarify 0.5.1 profile compatibility behavior.

## Impact

The compatibility manifest, two immutable schema resources, registry tests, release specs, and historical OpenSpec record are corrected. No document reader, schema shape, or wire-format migration is added.
