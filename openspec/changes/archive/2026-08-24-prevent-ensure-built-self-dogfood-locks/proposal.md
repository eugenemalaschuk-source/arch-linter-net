## Why

An installed CLI that runs `--ensure-built` against the ArchLinterNet repository can load
`ArchLinterNet.Testing.dll` before it starts its temporary graph build. On Windows that process-held
load prevents MSBuild from replacing the target output, so normal self-dogfood preparation fails
despite valid restored inputs.

## What Changes

- Route every `--ensure-built` validation through metadata-only project and artifact preparation
  before it starts the graph build, regardless of whether analysis caching is enabled.
- Materialize the isolated post-build assembly load scope only after the build and ordinary
  receipt verification complete.
- Preserve the metadata-selected output identity when refreshing receipts without an explicit
  configuration, framework, or runtime identifier, rather than selecting an unrelated newer
  output from another configuration.
- Add Windows-relevant CLI regression coverage for a self-analysis target that includes
  `ArchLinterNet.Testing`, plus isolated packaged-tool smoke coverage.
- Preserve existing build receipts, strict/audit findings, diagnostic projections, and explicit
  `--no-restore` behavior.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-build-state-preflight`: Explicit preparation must complete before the validating
  process loads a selected target artifact that the graph build may replace.

## Impact

Affected Core validation setup and snapshot materialization sequencing, CLI end-to-end regression
coverage, and packaged consumer smoke coverage. No policy format, public API, receipt schema, or
diagnostic schema changes are introduced.
