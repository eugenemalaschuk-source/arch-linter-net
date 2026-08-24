## Why

The existing self-analysis tests prove that `--ensure-built` can validate an existing output, but
they do not require MSBuild to replace the selected DLL. On Windows that leaves the original
file-lock defect untested, and the new post-build output-selection guard lacks fast branch-level
coverage.

## What Changes

- Make the installed-tool Windows regression force a source-driven rebuild of its selected
  `ArchLinterNet.Testing.dll`, then verify the output digest and receipt publication.
- Make the source-CLI regression select the fixture output explicitly and force a replacement.
- Add focused tests for prepared-output reuse and every validation fallback in post-build assembly
  resolution.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-build-state-preflight`: Explicit ensure-built preparation must prove that a selected
  output can be replaced and that the receipt binds the rebuilt bytes.

## Impact

Affected Core build-state tests, CLI and packed-package regressions, and the existing
`analysis-build-state-preflight` specification. No public API, policy syntax, receipt schema, or
normal validation behavior changes.
