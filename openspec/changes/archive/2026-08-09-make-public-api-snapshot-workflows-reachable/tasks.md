## 1. Preparation plumbing

- [x] 1.1 Extend public-API Core requests and shared surface resolution with explicit preparation and no-restore inputs.
- [x] 1.2 Recreate the public-API runner and verify the post-build artifact state after successful preparation.

## 2. CLI surface

- [x] 2.1 Add shared `--ensure-built` and `--no-restore` options to every live-surface public-API subcommand and forward them to Core.
- [x] 2.2 Update public-API help and documentation with the supported snapshot workflow.

## 3. Regression coverage

- [x] 3.1 Add Core tests for prepared re-resolution and ordinary stale/receiptless failure behavior.
- [x] 3.2 Add CLI option-forwarding and help coverage.
- [x] 3.3 Add installed/packed CLI acceptance coverage for capture → diff → update and stale receipt rejection.

## 4. Validation and synchronization

- [x] 4.1 Run focused tests, format the repository, and run full acceptance; fix issue-related failures.
- [x] 4.2 Synchronize implementation, documentation, and OpenSpec artifacts; validate and archive the change.
