## 1. Relay package integrity and dependency evidence

- [x] 1.1 Add the Relay third-party license notice and checked-in bundle
  manifest with exact source/asset digests and approved publisher pins.
- [x] 1.2 Include the notice and bundle manifest in the CLI package and make
  packaged/local Relay resolution validate the manifest before setup writes.
- [x] 1.3 Add focused packed/setup regression coverage for required assets,
  valid resolution, and tampered/incompatible bundle failure.
- [x] 1.4 Add bounded offline dependency-lock/license validation and runtime
  dependency audit coverage for the shipped Relay.

## 2. Frozen transport candidate tooling

- [x] 2.1 Expand the reviewed release inventory with explicit transport members,
  compatibility identities, and approved immutable workflow/action pins.
- [x] 2.2 Implement deterministic Relay distribution archive, compatibility
  metadata, transport manifest/checksums, strict verification, and attestation
  subject rendering by reusing the existing package manifest boundary.
- [x] 2.3 Add tests for deterministic output, missing/tampered/wrong-source/
  incompatible subjects, archive safety, and no recursive manifest hashing.

## 3. Release workflow and documentation integration

- [x] 3.1 Generate and verify the frozen transport candidate before upload and
  Checkpoint B consumption.
- [x] 3.2 Attest and independently verify transport subjects/evidence, and make
  release publication/attachment consume only verified manifest paths.
- [x] 3.3 Update release workflow structural tests and release documentation
  with the exact transport asset and #806 boundary.

## 4. Spec synchronization and validation

- [x] 4.1 Compare implementation with the two delta specs and update them for
  tested behavior.
- [x] 4.2 Run focused tests, formatter/lint checks, strict OpenSpec validation,
  and inspect the final scoped diff before PR creation.
