## Why

The #506 implementation left four correctness gaps identified by PR review: incomplete
collection integrity can look clean, duplicate evidence loses provenance, malformed duplicate
expectations are order-dependent, and a transport-only `pass` completion can mask an ordinary
architecture failure at the CLI boundary.

## What Changes

- Treat every expected identity with no produced record as `missing_applicability_record`
  integrity evidence; an intentionally absent optional input remains an explicit
  `not_applicable` produced record.
- Canonicalize duplicate expected entries and retain deterministic provenance for each duplicate
  expected or produced record.
- Require `ValidationOutcome.Passed` for a single-mode completion `pass` to exit `0`; preserve
  `fail` as `1` and `unassessable` as `2`.
- Add focused regressions for all four review findings and refresh the reviewed Core public API
  baselines only if the intended surface changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `governance-applicability-evidence`: Require complete deterministic joins and exact duplicate provenance for every expected record.
- `governance-assessment-completion`: Clarify that optional input absence requires an explicit produced `not_applicable` record.
- `cli-validation`: Prevent completion `pass` from overriding an ordinary failed validation outcome.

## Impact

Affected code is the Core applicability evaluator, the CLI exit mapper, focused Core/CLI tests,
and the reviewed OpenSpec specifications. No new package or public API is intended.
