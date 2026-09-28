## Why

The public v0.9.0 workflow pushed all NuGet packages and deployed docs, then created a published GitHub Release before uploading its candidate assets. GitHub's immutable-release protection rejected the first upload, leaving an incomplete public release. The release publisher must stage and verify assets while the release is still a draft.

## What Changes

- Create a missing GitHub Release as a draft and keep it unpublished while candidate assets are uploaded and read back.
- Publish the draft only after every expected candidate-bound asset matches its source digest.
- Resume matching drafts safely and fail closed for conflicting assets or incomplete published immutable releases.
- Document the draft-first release publication contract and cover it with release-tool and workflow tests.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `manual-nuget-release`: require release assets to be uploaded and digest-verified on a draft before an immutable GitHub Release is published.

## Impact

Affected code includes `tools/release/publish_release_assets.py`, its unit tests, release workflow contract tests, and `docs/reference/release-process.md`. The exact stable `0.9.1` release scope and its shipped-scope regression coverage bind the correction to the next v0.9.x maintenance candidate. No package API or dependency changes are expected.
