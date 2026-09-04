## MODIFIED Requirements

### Requirement: Checkpoint B subprocess cancellation bounds the process tree

Checkpoint B subprocess execution SHALL observe the NUnit cancellation token
while waiting for child processes and SHALL bound both process completion and
post-exit draining of redirected stdout and stderr. When cancellation, the
process-completion bound, or the post-exit drain bound fires, the gate SHALL
terminate the complete descendant process tree before completing the test so
timed-out `dotnet`, shell, MSBuild, or synthetic-consumer processes cannot
continue mutating temporary state after the test has ended.

The resulting cancellation or timeout failure SHALL identify the rendered
command, tracked root process id, elapsed duration, and phase, and SHALL retain
bounded stdout and stderr tails. A required composed Checkpoint B scenario
SHALL additionally preserve an ordered, bounded trace of completed packed-CLI
phases with their command identity and elapsed duration. If its NUnit watchdog
cancels the scenario, its failure diagnostics SHALL include that completed
phase trace and the currently executing phase, so a release run can distinguish
a slow command from accumulated orchestration cost. The trace is diagnostic
evidence only and SHALL NOT change scenario results, required shard inventory,
canonical platform evidence, or release authorization.

A composed scenario MAY reuse restored dependency state for an unchanged
synthetic fixture through the supported `--no-restore` option, but SHALL retain
the per-command build-state preparation guarantees required by its
`--ensure-built` command coverage.

On Windows, the root process SHALL be placed in its tracked job at creation
(not through a separate post-start assignment), and the scope SHALL retain a
cleanup mechanism that can terminate tracked descendants even after the root
process has exited; this after-root-exit guarantee is Windows-only. On
non-Windows platforms, descendant termination is guaranteed only while the
root process is still alive; a descendant that outlives its own root process is
outside the direct-tree fallback's reach, and the bounded post-exit drain wait
is the only bound that still applies. Locally packed Checkpoint B candidates
SHALL not reuse persistent `dotnet` build servers or MSBuild nodes.

#### Scenario: A child process owns a long-running descendant

- **WHEN** Checkpoint B cancellation fires while a subprocess tree is still
  running
- **THEN** the direct subprocess and its descendants terminate
- **AND** the test returns cancellation rather than waiting for the original
  child duration

#### Scenario: A descendant retains a redirected output handle after root exit (Windows)

- **WHEN** on Windows, the root subprocess exits but a descendant keeps stdout
  or stderr open past the post-exit drain bound
- **THEN** Checkpoint B terminates the tracked descendant process tree
- **AND** the bounded failure identifies the command, process id, drain phase,
  elapsed duration, and bounded output tails

#### Scenario: A composed scenario exhausts its watchdog

- **WHEN** a required composed Checkpoint B scenario receives its NUnit
  cancellation while one packed-CLI phase is executing
- **THEN** the failure identifies that active phase and its command
- **AND** it includes the ordered bounded timing trace for every phase that
  completed before cancellation
- **AND** the cancellation does not authorize a scenario, shard, platform, or
  release candidate

#### Scenario: Checkpoint B packs a local candidate

- **WHEN** the fixture creates its own candidate package feed
- **THEN** the packaging invocation disables persistent build-server and MSBuild
  node reuse
