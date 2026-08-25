## 1. Path-specific deterministic consumer-shaped evidence

- [x] 1.1 Run every covered metadata and public-API fan-out in a fresh session; verify the relevant materialization counter transitions `0 -> 1 -> 1` for that path alone across 24 projects and 16 contracts.
- [x] 1.2 Add a literal golden checksum and count for a non-empty ordered canonical result projection; verify the fixture no longer derives expected results from a second optimized execution.
- [x] 1.3 Add temporary-policy Testing API and CLI assertions proving strict/audit outcome and exit semantics; verify strict succeeds and audit returns the validation-failure exit code.

## 2. Evidence documentation

- [x] 2.1 Update the evidence documentation to describe path-isolated counters, the literal canonical checksum, and host-level outcomes; retain the explicit wall-clock/allocation and #502 exclusions.

## 3. Integration validation

- [x] 3.1 Run the focused fixture, changed Core test family, formatting, architecture lint, public-API review, and strict OpenSpec validation; verify all required local checks pass and inspect the final diff for no production or profile-schema change.
- [x] 3.2 Classify the temporary-project and CLI-subprocess fixture as E2E in both positive and unit-exclusion filters; add its NUnit category and a bounded, cancelling child-process lifetime.
- [x] 3.3 Run shard-membership validation plus the Core unit-shard and E2E buckets; repeat focused, formatting, and strict OpenSpec validation and inspect the final diff.
