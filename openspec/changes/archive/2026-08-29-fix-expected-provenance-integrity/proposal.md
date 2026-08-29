## Why

The applicability completion boundary accepts an expected entry whose provenance
can name a different family or control. That malformed authority can join to a
valid produced record and incorrectly report a trusted pass.

## What Changes

- Validate that every expected entry's provenance uses the same canonical family
  and effective-control identity as the entry itself.
- Treat a mismatch as deterministic collection-integrity evidence and derive
  `unassessable` completion rather than joining it as trusted input.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `governance-applicability-evidence`: Expected-entry provenance must be
  canonical and malformed provenance must fail closed.
- `governance-assessment-completion`: A malformed expected authority prevents a
  trusted completion result and exposes canonical integrity provenance.

## Impact

- `ArchitectureApplicabilityEvaluator` and its Core regression tests.
- The stable applicability integrity-reason vocabulary and reviewed Core public
  API snapshot.
