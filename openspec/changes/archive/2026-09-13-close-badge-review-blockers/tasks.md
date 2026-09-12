## 1. Contract and design

- [x] 1.1 Record the review blockers and add OpenSpec requirements for optional
  renewal output and exact bounded cadence schedules.
- [x] 1.2 Document the deterministic schedule-generation and migration design.

## 2. Implementation and regression coverage

- [x] 2.1 Emit the Relay renewal workflow and managed path only when renewal is
  enabled.
- [x] 2.2 Replace cadence rounding with exact daily UTC cron slots and preserve
  the cost-preview bound for non-hour-aligned cadences.
- [x] 2.3 Add executed tests for disabled renewal, enabled renewal, and 90/120
  minute schedule rendering and slot counts.

## 3. Validation and delivery

- [x] 3.1 Update the turnkey guide and archived/main specification as needed,
  run formatting, targeted tests, architecture/public API/docs/OpenSpec gates,
  and record the review resolution in issue #832 and PR #858.
