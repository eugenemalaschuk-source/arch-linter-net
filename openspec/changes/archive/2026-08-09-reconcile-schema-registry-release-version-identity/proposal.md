## Why

The `0.6.0` package line ships immutable schema identities under `0.5.1` while
its packaged README still presents `0.5.1` as the product release target. That
leaves adopters unable to tell whether the registry is stale or intentionally
independently versioned, and makes otherwise-valid `$schema` guidance appear
incorrect.

## What Changes

- Define the packaged schema registry as an independently versioned immutable
  compatibility contract, with an explicit mapping from the `0.6.0` package
  line to the shipped `0.5.1` schema identities.
- Update the packaged README and public schema/release guidance so the product
  release and schema-registry version cannot be mistaken for one another.
- Add release-package checks that compare the packed CLI version, schema list,
  packaged README, and release-facing schema guidance, rejecting stale public
  release-target wording or unsupported schema URLs.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `packaged-schema-registry`: Declare independent registry/product version
  mapping and require release-artifact consistency validation.
- `adoption-stabilization-compatibility`: Clarify the supported package-line
  relationship to the immutable schema registry.
- `docs-site`: Require release-facing documentation to identify the current
  product release separately from its supported immutable schema identities.

## Impact

Affected areas are `PackagedSchemaRegistry`, package assets and validation,
CLI/Core tests, the root packaged README, public schema/reference/release
documentation, and the corresponding OpenSpec contracts. There are no new
runtime commands, dependencies, or schema-format revisions.
