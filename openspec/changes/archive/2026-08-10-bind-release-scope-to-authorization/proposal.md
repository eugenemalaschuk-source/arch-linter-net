## Why

Review of the packed-artifact 0.6.1 gate found three ways it could authorize publication without
proving what issue #466 requires.

- Authorization was computed from scenario failures and policy-shape defects only. #466 requires
  every required item under story #434 to be closed before PASS, and requires the closed
  release-scope inventory to be part of the emitted evidence. A reopened F1–F11/#465 item, or a
  new release-blocking defect filed under the story, could still produce PASS.
- The F3 scenario captured a snapshot, diffed it clean, dry-ran an update, and then checked only
  the stale-receipt path. It never added, removed, or changed an exported signature, so it could
  not detect a regression in any of the three delta classes #466 names.
- The F2 scenario ran only the installed CLI and hashed only assemblies. #436 was reproduced
  through project evaluation that the Testing API drives too, so the packaged
  `WithEnsureBuilt()` path must also survive back-to-back validations, and a torn PDB breaks a
  consumer's `dotnet test --no-build` just as badly as a torn assembly.

## What Changes

- Declare the authoritative release scope in the repository, resolve the current issue state
  bound to the candidate manifest and source commit, refuse PASS while any required item is open,
  and emit the inventory in the JSON and Markdown evidence.
- Extend the F3 scenario to add, remove, and change exported signatures, assert all three delta
  classes from the installed candidate, and prove `update` restores snapshot sync.
- Extend the F2 scenario with a packaged `ArchLinterNet.Testing` consumer performing two
  back-to-back `WithEnsureBuilt()` validations without an intervening rebuild, and widen the
  preservation oracle from assemblies to every selected primary output.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `checkpoint-b-release-evidence`: bind publication authorization to the authoritative
  release-scope closure inventory, and strengthen the F2 and F3 consumer-cleanup oracles.

## Impact

Affected areas are the packed-artifact gate scenarios, the release-evidence aggregation tool and
its regressions, a new release-scope declaration and resolver, and the release workflow. No
product runtime, public API, or schema format changes.
