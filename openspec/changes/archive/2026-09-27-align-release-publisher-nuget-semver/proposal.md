## Why

Candidate preparation accepts the complete NuGet package-version syntax exposed by `version_override`, and history-forensics now shares that parser. The GitHub Release publisher still applies a narrower stable/preview-only tag regular expression, so valid candidates can fail during publication.

## What Changes

- Validate release tags with the shared package-version parser.
- Preserve the required canonical `v` tag prefix while accepting all versions accepted by `version_override`, including `alpha`, `rc`, and build metadata.
- Add publisher regression tests for the accepted version forms.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `manual-nuget-release`: make GitHub Release identity validation consistent with the package-version contract.

## Impact

The release asset publisher, its tests, and the manual release specification. Release-scope authorization and package publication policy remain unchanged.
