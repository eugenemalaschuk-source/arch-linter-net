## 1. Bounded Checkpoint B process execution

- [x] 1.1 Replace the unbounded redirected-stream waits with a bounded process and post-exit drain scope that returns normal command output and emits command, PID, phase, elapsed-time, and bounded-tail diagnostics on timeout; verify focused process-runner tests pass.
- [x] 1.2 Attach Windows child processes to a kill-on-close job scope and retain non-Windows process-tree cleanup for live roots; add inherited-handle and cancellation regressions proving the runner completes within its configured bounds and leaves tracked descendants stopped.

## 2. Candidate packaging and validation

- [x] 2.1 Disable `dotnet` build-server reuse and scope MSBuild node reuse off for locally packed Checkpoint B candidates; verify the focused candidate-package Checkpoint B path still creates and consumes its isolated feed.
- [x] 2.2 Run format, focused process-runner and Checkpoint B candidate tests, Core-suite validation, relevant lint/OpenSpec checks, and record the results.
