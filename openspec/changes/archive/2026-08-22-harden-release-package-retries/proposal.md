## Why

Review found that Windows Checkpoint B interprets Bash environment syntax in a
PowerShell step, and a duplicate primary NuGet push can conceal an omitted
symbol package. Both permit an invalid release contract.

## What Changes

- Run manifest verification under Bash on every platform that uses Bash
  environment-variable syntax.
- Fail closed on a duplicate primary NuGet package instead of treating it as a
  successful rerun that may omit its adjacent `.snupkg`.
- Add executable workflow-contract tests and document retry handling.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `ci-release-gate`: enforce platform-correct verification shells and
  fail-closed publication retries for paired package subjects.
- `release-process-documentation`: document partial-publication retry handling.

## Impact

Changes are limited to CI/release workflow YAML, release workflow tests, and
maintainer documentation.
