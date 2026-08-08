## Why

The packaged policy schema requires `roots` for project- and assembly-scope coverage
contracts, but the runtime validator correctly rejects that field because both scopes
classify all discovered units. This makes valid strict coverage policies impossible to
validate directly or after import composition.

## What Changes

- Align the policy-root schema, which the fragment schema references, with the
  runtime rule: project and assembly coverage contracts reject `roots`.
- Preserve namespace coverage's required namespace `roots` and the existing
  discriminated fields for dependency-edge, rule-input, and semantic-role coverage.
- Add direct and composed-policy regressions that exercise every supported coverage
  scope and the packaged schema artifact used by `policy check`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-coverage-model`: Coverage scope field rules distinguish discovery-wide
  project/assembly coverage from namespace-rooted coverage.
- `policy-import-composition`: Composed policies retain the same coverage-schema
  validity as direct policies.
- `policy-check-command`: Packaged-schema policy checks accept valid discovery-wide
  coverage contracts and reject invalid roots actionably.

## Impact

- `schema/dependencies.arch.schema.json` and the fragment schema embedded in the
  packages.
- CLI and Core policy-validation fixtures, including package-artifact validation.
- Coverage-contract and policy-composition specifications.
