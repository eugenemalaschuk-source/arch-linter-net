## Why

The manual release workflow requires maintainers to type an explicit NuGet package version. This is error-prone: a typo can create an invalid version, skip an intended version, or publish a release with a version that does not follow the repository release sequence. Replacing free-text input with a dropdown of release scenarios reduces mistakes and makes the release process faster and more predictable.

## What Changes

- Replace the `version` free-text workflow input with a `release_type` choice dropdown supporting `preview`, `patch`, `minor`, and `major` scenarios.
- Add an optional `version_override` text input as an emergency/manual recovery path, visibly separated from the normal dropdown path.
- Create a Python script `tools/release/calculate_version.py` that detects the latest SemVer-compatible git tag and calculates the next `PACKAGE_VERSION` from the selected release type.
- Create Python unit tests under `tests/release/` for the version detection and increment logic.
- Update the release workflow to use the calculated version instead of the raw input.
- Update `docs/reference/release-process.md` to document the new dropdown-driven release flow.

## Capabilities

### New Capabilities
- `release-version-bump`: Automated version detection from git tags and deterministic increment logic for preview, patch, minor, and major release scenarios.

### Modified Capabilities
- `manual-nuget-release`: The release workflow input changes from free-text `version` to `release_type` dropdown plus optional `version_override`. The spec must be updated to reflect the new input contract.

## Impact

- `.github/workflows/release-nuget.yml`: workflow input definition changed; version calculation step added
- `tools/release/calculate_version.py`: new file — SemVer tag detection and version calculation
- `tests/release/test_calculate_version.py`: new file — unit tests
- `docs/reference/release-process.md`: updated to describe dropdown-driven release flow
- `.gitignore`: may need to exclude Python `__pycache__` in `tools/` and `tests/release/`
