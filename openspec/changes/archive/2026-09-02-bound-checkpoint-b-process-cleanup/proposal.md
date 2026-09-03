## Why

Checkpoint B can hang after `dotnet pack` exits when a descendant process retains
a redirected output handle. The release-blocking fixture must instead fail within
a known bound, retain useful diagnostic evidence, and clean up the process tree.

## What Changes

- Bound child-process completion, post-exit output draining, and cleanup waits in
  the Checkpoint B process runner.
- Emit actionable timeout diagnostics containing the command, process id, timed
  out phase, elapsed duration, and bounded stdout and stderr tails.
- Hold Windows descendants in a job object so cleanup remains reliable after the
  tracked root process exits; retain explicit tree cleanup on other platforms.
- Invoke locally packed candidates without persistent `dotnet` build servers or
  reusable MSBuild nodes.
- Add a regression fixture for a descendant that inherits redirected output.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `checkpoint-b-release-evidence`: Checkpoint B subprocess execution gains
  bounded post-exit stream draining, actionable timeout evidence, and reliable
  descendant cleanup.

## Impact

The change is confined to the Core test project's Checkpoint B harness and its
NUnit regressions. It does not alter production release, package, provenance,
or Checkpoint B scenario/evidence semantics, and introduces no public API or
runtime dependency.
