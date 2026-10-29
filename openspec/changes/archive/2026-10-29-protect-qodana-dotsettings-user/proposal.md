# Proposal

## Why

Review of PR #1119 found that `.DotSettings.user` files are not covered by the publisher's existing `.DotSettings` suffix check. If Qodana reads a ReSharper user-layer settings file, a pull request could change its analysis configuration without triggering the protected-input governance path.

## What Changes

- Protect nested `.DotSettings.user` files in the trusted Qodana publisher, case-insensitively.
- Add a regression contract for the user-layer suffix and ensure unrelated `.user` names are not overmatched.
- Update Qodana adoption guidance to name both protected ReSharper settings suffixes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `qodana-community-ci`: include `.DotSettings.user` among protected analysis visibility inputs.

## Impact

The trusted publisher's scan-scope path guard, its Python contract tests, Qodana adoption documentation, and the existing Qodana CI specification are affected. No scanner permissions, finding thresholds, or publisher trust boundaries change.
