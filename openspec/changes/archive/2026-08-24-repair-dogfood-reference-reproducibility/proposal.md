## Why

PR #641's real-repository reference does not yet reproduce the released v0.7.0
consumer tool independently of the repository's own v0.6.0 tool manifest. It
also names a canonical-report digest without retaining the report bytes, and
its `--enrich-dotnet` result is environment-dependent.

## What Changes

- Install and invoke the recorded tool from an isolated, exact-version tool
  directory throughout the public workflow.
- Retain the canonical Git-only JSON report beside its evidence record, and add
  a documentation lint that verifies the retained bytes against the documented
  SHA-256.
- Make `not_requested` the canonical enrichment projection; document .NET
  enrichment as a separate advisory observation with no canonical digest claim.
- Update the evidence record and public guide with the verified consumer smoke
  path and explicit trust boundaries.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `self-dogfood-reference-workflow`: Require a retained, checksum-verified,
  environment-independent canonical report and an exact isolated tool entrypoint.

## Impact

Affected areas are the public workflow guide, contributor evidence, a checked-in
JSON artifact, documentation linting, and the self-dogfood reference OpenSpec
contract. There are no runtime, public API, policy, or baseline changes.
