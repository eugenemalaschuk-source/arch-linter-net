## 1. Trust inputs and live OIDC

- [x] 1.1 Lock publisher/action pins to the shipped v1 contract in the engine,
  writer, and schema; add malicious-pin rejection tests.
- [x] 1.2 Make unsigned `--capability-evidence` fail closed and update setup,
  doctor, packed, and guide coverage.
- [x] 1.3 Verify OIDC JWT signatures against the fixed GitHub Actions JWKS before
  accepting claims; add valid-signature and dummy-signature regression tests.

## 2. Independent producer and safe persistence

- [x] 2.1 Generate the trusted NuGet config and install the CLI before PR
  checkout with an explicit `--configfile`; test a malicious repository config
  cannot influence the generated workflow installation path.
- [x] 2.2 Apply the reparse-point containment guard to all managed output reads,
  temporary writes, and rollback; add linked `.github` and Relay tests.

## 3. Contract and validation

- [x] 3.1 Update the guide and active delta spec to describe the authenticated
  live-inspection boundary and fixed v1 pins.
- [x] 3.2 Run focused tests, format/architecture/schema/docs/OpenSpec checks,
  perform a fresh post-fix bypass review, commit and push PR #858, and publish
  the expanded issue/PR status without changing the exact task-matching title.
