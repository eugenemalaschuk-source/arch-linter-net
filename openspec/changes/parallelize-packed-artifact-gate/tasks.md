# Tasks

- [x] Split the monolithic Checkpoint B orchestrator into deterministic scenario shards and remove duplicate oracle calls.
- [x] Reuse one candidate in the complete local fixture via one-time setup/teardown.
- [x] Add isolated shard Make targets and an ephemeral immutable-candidate preparation target for PR CI.
- [x] Add fail-closed shard-evidence merge tooling and regression tests.
- [x] Preserve stable required packed-artifact PR check names as fan-in checks.
- [x] Parallelize Checkpoint B shards across the PR Windows/macOS platforms.
- [x] Parallelize Checkpoint B shards across all release platforms while preserving canonical platform evidence.
- [x] Stop release stages from rerunning packed-artifact proof through generic repository acceptance.
- [x] Make subprocess waits cancellation-aware and kill descendant process trees.
- [ ] Validate workflow syntax/security/formatting and focused unit/tooling tests in PR CI.
- [ ] Record real before/after PR timings, including the slowest Windows shard and stable fan-in critical path.
- [ ] Record the next release-workflow timing evidence and verify only the immutable-candidate Checkpoint B matrix executes packed-adopter proof.
