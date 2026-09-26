## Why

The release candidate workflow accepts NuGet SemVer overrides beyond `X.Y.Z-preview.N`, but history-forensics currently rejects those candidates after packaging. Its recorded bundle-render duration also includes the analyzer process, making the operational measurements misleading.

## What Changes

- Make release-history range selection accept the complete package-version surface validated by `calculate_version.py`.
- Define one prerelease series per `X.Y.Z`, ordered with NuGet SemVer precedence; retain build metadata in candidate identity while excluding it from precedence.
- Measure runner-side bundle rendering after the analyzer finishes so its duration is separate from analyzer process wall time.
- Update release documentation and regression coverage for `alpha`, `rc`, and build-metadata candidates.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `manual-nuget-release`: align predecessor selection and operational timing with the accepted candidate version and measurement contracts.
- `release-process-documentation`: document the generic prerelease-line and build-metadata rules.

## Impact

Release version parsing/range resolution, transport bundle validation, operational observations, focused Python tests, OpenSpec specs, and maintainer release-process documentation. Candidate version calculation and package publication authority remain unchanged.
