## Why

Issue #436 reproduced a destructive interaction between explicit build-state
preparation and Buildalyzer's post-build MSBuild evaluation. A successful
`--ensure-built` validation can remove or tear selected primary artifacts,
breaking consumers that correctly continue with `dotnet test --no-build`,
packaging, or another Testing API validation.

## What Changes

- Make Buildalyzer-backed project evaluation preserve selected primary build
  artifacts after `--ensure-built` or `WithEnsureBuilt()` completes.
- Retain receipt-based artifact verification and fail-closed preflight
  semantics, including the post-build verification pass.
- Add CLI and Testing API regressions that verify artifact bytes and support
  sequential in-process validations without a consumer rebuild.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-build-state-preflight`: explicit ensure-built preparation leaves
  verified build artifacts in a coherent, consumable state.
- `project-aware-roslyn-analysis`: its MSBuild-backed context evaluation does
  not destructively mutate already-built primary outputs.

## Impact

- `src/ArchLinterNet.Core/Discovery/`: Buildalyzer environment options for
  project-aware evaluation.
- `tests/ArchLinterNet.Core.Tests/`: real build-state, CLI, and Testing API
  regression fixtures.
- `openspec/specs/`: synchronized behavior guarantees for build-state
  preparation and project-aware Roslyn evaluation.
