## Why

Review found that repeated layer-template expansion breaks reference-identity
provenance, and the new CLI error path omits related SARIF locations and emits
a JSON schema inconsistent with normal CI artifacts.

## What Changes

- Bind provenance to the actual catalog expansion instances.
- Include primary and related locations in SARIF policy-error results.
- Reuse the normalized snake_case policy-location JSON shape for exceptions.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `policy-import-composition`: expanded contracts must retain provenance across
  catalog execution.
- `violation-reporting`: policy exceptions must use consistent structured
  locations in JSON and SARIF.

## Impact

Provenance indexing, catalog construction, CLI error formatting, SARIF output,
and regression tests are affected. Import grammar and contract DTOs are not.
