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
bounded stdout and stderr tails. On Windows, the scope SHALL retain a cleanup
mechanism that can terminate tracked descendants even after the root process
has exited. Locally packed Checkpoint B candidates SHALL not reuse persistent
`dotnet` build servers or MSBuild nodes.

#### Scenario: A child process owns a long-running descendant

- **WHEN** Checkpoint B cancellation fires while a subprocess tree is still
  running
- **THEN** the direct subprocess and its descendants terminate
- **AND** the test returns cancellation rather than waiting for the original
  child duration

#### Scenario: A descendant retains a redirected output handle after root exit

- **WHEN** the root subprocess exits but a descendant keeps stdout or stderr
  open past the post-exit drain bound
- **THEN** Checkpoint B terminates the tracked descendant process tree
- **AND** the bounded failure identifies the command, process id, drain phase,
  elapsed duration, and bounded output tails

#### Scenario: Checkpoint B packs a local candidate

- **WHEN** the fixture creates its own candidate package feed
- **THEN** the packaging invocation disables persistent build-server and MSBuild
  node reuse
