## 1. Failure diagnostics

- [x] 1.1 Add bounded full-cycle packed-command phase timing and active-phase cancellation diagnostics; verify focused NUnit tests prove ordered trace capture and watchdog-failure output.
- [x] 1.2 Preserve existing process-runner cancellation/timeout diagnostics and verify its focused regression family still passes.

## 2. Trace-supported remediation

- [x] 2.1 Run the v0.8 packed full-cycle path to capture phase timing, record the root cause, and verify the trace identifies the active command and completed-phase durations.
- [x] 2.2 Remove only trace-proven redundant full-cycle restore work while retaining per-command `--ensure-built`, required command, and evidence assertions; verify focused full-cycle coverage passes without a watchdog change.

## 3. Synchronization and validation

- [x] 3.1 Synchronize the OpenSpec delta with the implemented diagnostics and run OpenSpec validation successfully.
- [x] 3.2 Run formatting, focused diagnostics tests, the Core test suite, architecture lint, and the packed v0.8 full-cycle target; record exact results for the pull request.
