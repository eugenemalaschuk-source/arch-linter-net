## 1. Post-build authorization

- [x] 1.1 Refresh the selected artifact closure's DLL, PDB, and receipt digests after a successful ensure-built graph build without re-running timestamp-based output discovery.
- [x] 1.2 Run ordinary receipt preflight against the refreshed selection before lazy materialization and retain prepared provenance.

## 2. Regression coverage and validation

- [x] 2.1 Update focused snapshot orchestration coverage to prove post-build authorization no longer re-prepares project output metadata.
- [x] 2.2 Retain and run the policy-selected Release ensure-built regression, then run formatting, relevant Core checks, and OpenSpec validation.
