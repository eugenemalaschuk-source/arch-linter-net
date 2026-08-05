## Why

The 0.5.1 packaged registry deliberately deferred three machine-readable formats until their owning work supplied real writers and generated-output validation. Those writers and source contracts now exist, so an installed package must expose the complete release-qualified registry without relying on a checkout or network access.

## What Changes

- Enroll the implemented `finding/v1`, `analysis-cache/v1`, and `analysis-profile/v1` contracts in the immutable 0.5.1 registry.
- Ship their exact source bytes as Core embedded resources and NuGet content files, with digests and identities verified at runtime.
- Prove generated finding, cache, and profile output validates against the packaged contracts, including an installed-package offline smoke test.
- Synchronize schema documentation and capability metadata with the complete registry.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `packaged-schema-registry`: Publish the three now-implemented machine-readable formats and verify their package/offline contract.
- `adoption-stabilization-compatibility`: Define the complete 0.5.1 release registry after deferred format owners have landed.

## Impact

Affected areas include the Core schema registry and package project, compatibility manifest, registry/package tests, documentation/capability inventory, and the two release-contract OpenSpec capabilities. No producer format, network service, or automatic migration is introduced.
