## 1. Core assessment-completion boundary

- [x] 1.1 Add immutable Core applicability membership, record, reason/provenance, and completion types with deterministic validation/ordering; verify focused Core tests cover required, optional, missing, duplicate, orphan, and incompatible records.
- [x] 1.2 Wire canonical applicability inputs and derived completion additively through contract execution and `ValidationOutcome`, preserving pre-v0.8 empty-input behavior; verify the shared-validation focused tests pass.
- [x] 1.3 Update the reviewed Core public-API snapshot for the approved additive surface and verify `make public-api-check` passes.

## 2. Host and Testing transport

- [x] 2.1 Map completion evidence through `ArchLinterNet.Testing` and improve its unassessable failure detail without manufacturing violations; verify the focused Testing adapter tests pass.
- [x] 2.2 Map successful authoritative CLI completion to the established `0`/`1`/`2` categories and additive Human/JSON/SARIF status evidence while retaining existing invalid-input/runtime/cancellation routing; verify focused CLI handler tests cover trusted pass, trusted failure, unassessability, output parity, and invalid invocation distinction.

## 3. Contract synchronization and validation

- [x] 3.1 Compare the implementation with the fail-closed governance OpenSpec deltas and update any incomplete or inaccurate behavior claim; verify `openspec validate fail-closed-governance-inputs --strict` passes.
- [x] 3.2 Run risk-appropriate Core, Testing, and CLI test projects plus formatter, architecture, API, and full OpenSpec checks; inspect the final diff before archiving the synchronized change.
