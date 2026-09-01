## Why

`Main Quality Telemetry` successfully uploads the Sonar analysis but then rejects its own
runner-created scanner log because `$RUNNER_TEMP` is outside the checkout. The verifier must
distinguish that trusted, read-only workflow input from repository-controlled paths without
weakening the release workspace confinement established by #743.

## What Changes

- Add an explicit, environment-anchored trust boundary for a read-only file under the current
  GitHub Actions `RUNNER_TEMP` root.
- Use that boundary only for `verify-sonar`'s `--scanner-log` input.
- Preserve `_safe_path` confinement for the coverage inventory, Sonar analyses response, and all
  other repository-controlled paths.
- Add regression tests for accepted runner-temp logs and rejected arbitrary or mismatched
  external paths while retaining the current fail-closed Sonar revision and coverage-import checks.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `release-tooling-workspace-confinement`: define the narrow `RUNNER_TEMP` trust boundary for
  read-only workflow verification inputs.

## Impact

Affected code is limited to the shared release-workspace path validator, the main-quality Sonar
verifier, its focused Python tests, and the associated OpenSpec contract. No product API, package,
or external-service quality-gate semantics change.
