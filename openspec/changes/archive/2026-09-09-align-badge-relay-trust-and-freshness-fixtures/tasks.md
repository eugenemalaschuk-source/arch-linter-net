## 1. Align executable trust and profile contracts

- [x] 1.1 Remove fixture key snapshots and rotation schedules; make unknown
  `kid` behavior refer to one bounded refresh of the fixed GitHub JWKS endpoint.
- [x] 1.2 Restrict the fixture registry event/ref to `push` and
  `refs/heads/main`.
- [x] 1.3 Add a schema-constrained `headline-plus-freshness/v1` golden
  representation and bind its accepted vector to its exact bytes and digest.

## 2. Validate and archive

- [x] 2.1 Run JSON/schema/digest/vector, documentation, and strict OpenSpec
  validation; archive the synchronized delta and verify all specs.
