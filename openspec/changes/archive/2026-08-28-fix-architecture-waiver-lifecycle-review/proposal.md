## Why

Post-implementation review of #687 found correctness gaps in the initial
waiver lifecycle delivery: source-expanded waivers can be rejected or marked
stale incorrectly, `invalid` has no canonical evidence path, and accepted
fingerprint text is not fully canonical. Human output also omits required
structured-waiver target and remediation fields.

## What Changes

- Preserve one authored waiver declaration across source-set-expanded contract
  instances, while aggregating its matches before calculating lifecycle state.
- Represent malformed manual waivers as deterministic, fail-closed `invalid`
  lifecycle evidence instead of only an opaque loading failure.
- Require the canonical lowercase SHA-256 fingerprint representation at schema
  and validation boundaries.
- Include the structured target fingerprint and remediation reason in human
  lifecycle diagnostics.
- Add focused source-set, invalid-state, canonicalization, and output
  regression coverage.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-waiver-lifecycle`: Correct lifecycle aggregation, invalid
  evidence, fingerprint canonicalization, and human diagnostic requirements.
- `ignore-matching`: Ensure exact structured matching remains correct for
  source-set-expanded contract instances.

## Impact

Changes are confined to Core policy validation, source expansion/lifecycle
evaluation, reporting, schema, tests, documentation, and the existing #705 PR.
No new external services, package dependencies, or downstream inventory/Health
ownership are introduced.
