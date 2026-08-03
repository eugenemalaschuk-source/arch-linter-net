## Why

`analysis-build-state/v1` currently uses a coarse, directory-based input digest that is adequate for ordinary stale-artifact detection but cannot authorize persistent cross-process cache reuse. It does not prove the complete evaluated MSBuild input set, so #365 needs a deterministic manifest or an explicit refusal to cache.

## What Changes

- Add a bounded, deterministic evaluated build-input manifest for each selected project/output context.
- Represent an explicit cache-eligibility outcome and typed reasons; unknown or uninspectable inputs fail closed as `cache-ineligible`.
- Bind receipt verification to the manifest and expected PE/PDB/deps/runtimeconfig bytes, while preserving the existing distinction between build and policy/session identity.
- Expose the same eligibility and invalidation evidence to Core, CLI, and Testing API machine-readable diagnostics/profile counters.
- Add focused coverage for linked inputs, imported targets, context identities, reference changes, portable checkouts, and unsupported inputs.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `analysis-build-state-fingerprints`: require a complete-or-cache-ineligible evaluated manifest and portable cache-authorization evidence.
- `analysis-build-state-preflight`: expose the evaluated manifest eligibility result consistently with preflight and receipt verification.

## Impact

Affected areas include `ArchLinterNet.Core.BuildState`, build receipts and preflight diagnostics, snapshot/profile projections, CLI and Testing API surfaces, their NUnit coverage, and the analysis/build-state reference documentation. No persistent cache is introduced by this change.
