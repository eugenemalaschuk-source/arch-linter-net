## Why

The opt-in `health --profile` output currently contains deterministic counters but no timed phases, even though Health can run the same immutable analysis snapshot and validation timings used by `validate --profile`. This leaves normalized dogfood Health runs unable to distinguish preparation from contract evaluation, so long consumer CI spans cannot be attributed from the profile itself.

## What Changes

- Pass one `ValidationTiming` through the Health snapshot preparation and strict/audit evaluation when `--profile` is requested.
- Include the existing profile phase timings and process measurements in `analysis-profile/v1` output for Health, including the composite `--change-snapshot` path.
- Preserve default Health output, exit categories, findings, and profile schema; timing remains opt-in through the existing `--profile` option.
- Retain the profile from the existing architecture Health CI projection as a separate optional artifact.
- Extend focused tests and the phase dictionary to cover Health profiling.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-profile`: Health profiles include measured snapshot preparation and evaluation phases when profiling is requested.

## Impact

- Affected code: Core Health orchestration, CLI Health handler, shared analysis-profile publisher, CI Health artifact workflow, and focused CLI/Core tests.
- No public API, schema shape, dependency, policy, or validation-result change is intended.
- Internal synthetic attribution evidence remains the source for public-safe performance claims; private adopter identifiers and raw logs are excluded.
