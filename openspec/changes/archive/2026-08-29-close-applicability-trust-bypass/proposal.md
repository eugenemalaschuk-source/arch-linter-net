## Why

The execution result currently accepts a pre-derived completion value that can
override empty canonical applicability collections. That lets a family bypass
the fail-closed expected-to-produced join. In addition, an `unassessable`
record can carry a reason with unrelated provenance into machine-readable and
human-facing output.

## What Changes

- Derive assessment completion only from the canonical expected-membership and
  produced-record collections at the snapshot trust boundary.
- **BREAKING** Remove the pre-derived completion value from the contract
  execution-result transport model.
- Reject an `unassessable` record whose reason provenance does not exactly
  match the record's canonical family, control, and policy provenance.
- Add regressions for both rejected evidence paths.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `governance-applicability-evidence`: Applicability reason provenance must be
  canonical and collection evidence must remain the sole completion input.
- `governance-assessment-completion`: Completion aggregation must not accept
  executor-supplied pre-derived evidence and must surface malformed reason
  provenance as unassessable integrity evidence.

## Impact

- Affects Core execution-result transport, snapshot completion derivation, and
  applicability evaluation.
- Updates Core public API snapshots because the unsafe transport property is
  removed.
- Adds focused NUnit coverage; no new dependencies or configuration are
  required.
