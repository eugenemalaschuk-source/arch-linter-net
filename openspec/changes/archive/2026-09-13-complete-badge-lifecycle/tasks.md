## 1. Relay lifecycle and diagnostics

- [x] 1.1 Add the bounded lifecycle contract helpers, compatibility allowlist, and redacted status model.
- [x] 1.2 Extend the private registry with atomic metadata rename/rotation and tombstone-aware lookup.
- [x] 1.3 Add Durable Object admin status/invalidate/revoke seams, additive migrations, and bounded operation journal.
- [x] 1.4 Add outer Worker admin routes for lifecycle operations with revoke-before-cleanup ordering and generic errors.

## 2. CLI and operator workflow

- [x] 2.1 Add the authenticated `badge architecture-health lifecycle` command and dry-run planner.
- [x] 2.2 Wire the single badge command definition and update CLI help/docs.
- [x] 2.3 Add the executable lifecycle runbook with lifecycle matrix, compatibility, cost, retention, and synthetic checks.

## 3. Verification and closure

- [x] 3.1 Add Relay contract/integration tests for identity transitions, races, pin rotation, upgrade/rollback, status redaction, and retention.
- [x] 3.2 Add CLI tests for configuration/token/dry-run behavior and bounded diagnostics.
- [x] 3.3 Run focused Relay/CLI tests, formatter/lints, OpenSpec validation, synchronize/archive the change, and inspect the final diff.
