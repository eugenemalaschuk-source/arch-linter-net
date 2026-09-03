## Context

See proposal.md for motivation. The Checkpoint B runner starts processes with
redirected streams, waits for the root process, then waits without a bound for
both stream reads. A descendant can inherit a pipe handle, letting the root
exit while the stream reads never reach EOF. `Process.Kill(entireProcessTree)`
is insufficient on Windows once that root process exits because the remaining
descendants can no longer be reached through the root process handle.

## Goals / Non-Goals

**Goals:**

- Bound every runner wait, preserve diagnostics, and clean up the tracked work.
- Make Windows cleanup independent of the root process still being alive.
- Keep cancellation as `OperationCanceledException` and avoid changing normal
  command results or Checkpoint B evidence semantics.
- Prove the root-exits/descendant-holds-pipe case deterministically.

**Non-Goals:**

- Change production process execution, package provenance, or release workflow
  authority.
- Increase NUnit's global fixture timeout or weaken scenario assertions.
- Capture unbounded process output or introduce a general process-runner API.

## Decisions

### One bounded runner scope owns process lifetime and both stream drains

The Checkpoint B runner will start stdout/stderr reads immediately, apply a
bounded wait to root-process completion, and separately apply a bounded
post-exit wait for both read tasks. Output capture remains complete for normal
commands; timeout diagnostics keep only a fixed tail per stream. This separates
the observable failure phases without applying a global NUnit timeout.

On a process, cancellation, or drain timeout, the scope first requests tracked
tree cleanup, then waits only for the cleanup/drain bound. Cancellation is
re-thrown as cancellation; timeout is an assertion-oriented failure carrying
the command, PID, phase, elapsed duration, and stream tails.

### Use a Windows kill-on-close job scope; retain process-tree cleanup elsewhere

On Windows, the root process is placed in a kill-on-close job object
atomically at creation, via `STARTUPINFOEX` and
`PROC_THREAD_ATTRIBUTE_JOB_LIST` passed to `CreateProcessW`, rather than
through a separate `AssignProcessToJobObject` call after `Process.Start`
returns. This closes the window a post-start assignment would leave open: a
descendant spawned between start and assignment could otherwise escape the
job, and a failed assignment could leave an already-running root untracked.
Closing the job scope terminates the process group even when the root
`dotnet` process already exited and a descendant retains the redirected
handle. On non-Windows platforms the existing direct
`Kill(entireProcessTree: true)` approach remains the reviewable fallback, and
only while the root process itself is still alive: a descendant that outlives
its own root process is outside that fallback's reach on non-Windows.

Job objects were selected over post-exit child enumeration because they are
attached before descendants are created and keep an operating-system-owned
membership boundary. A polling process-table implementation would be racy and
platform-specific. An external `taskkill` helper would add shell-dependent
failure modes.

### Candidate packaging opts out of reusable build infrastructure

The test-only candidate `dotnet pack` command will pass
`--disable-build-servers` and an invocation-scoped MSBuild node-reuse setting.
This narrows the persistent-process risk without modifying production release
or CI workflow commands.

### Regression testing uses a root-exit inherited-handle probe

The new process-runner regression creates a root process that writes its child
PID, launches a descendant inheriting redirected streams, and exits. It asserts
that the runner ends within the configured bound, reports the drain-phase
diagnostic fields, and leaves the child dead. The existing cancellation test
continues to prove the still-running root/descendant path. A focused assertion
also inspects the candidate-pack invocation construction.

## Risks / Trade-offs

- [A process ignores termination or a stream callback lingers] → cleanup and
  drain waits stay independently bounded; diagnostics name the phase that did
  not settle.
- [Job assignment is unavailable in a constrained Windows environment] → fail
  with the Win32 error before executing untracked work rather than claim reliable
  cleanup.
- [Timeouts are too short on slower CI runners] → constants are local to the
  fixture runner and comfortably exceed normal command completion; only the
  synthetic retained-handle probe exercises the short configured bound.
- [Output is very large during failure] → tail capture limits diagnostic memory
  while normal successful output remains untruncated.

## Migration Plan

1. Add the bounded process scope and Windows job-object ownership.
2. Route all Checkpoint B subprocess paths and local candidate packing through
   it, with server reuse disabled for packing.
3. Add regression coverage, run focused and Core validation, and synchronize
   the specification.

Rollback is a single test-harness change: reverting it restores the former
runner without altering shipped packages, public APIs, or release artifacts.
