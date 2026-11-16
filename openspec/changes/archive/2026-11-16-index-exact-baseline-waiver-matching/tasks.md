# Tasks

## 1. Establish the current matcher baseline

- [x] 1.1 Add a focused evidence fixture and record baseline revision, independent `I`/`F`/`K`/wildcard workloads, deterministic scan/fingerprint work, environment-labelled timing, allocations, retained-memory method, and the measured or explicitly not-measured phase-share bound; verify it by running the explicit benchmark on the unchanged matcher.

## 2. Index matching per contract

- [x] 2.1 Build and integrate the immutable per-contract exact identity, waiver fingerprint, and literal-pair lookups with ordered wildcard fallback and cancellation checks; verify existing ignore, baseline, waiver, and unmatched-ignore tests pass.
- [x] 2.2 Add deterministic randomized differential tests against the original ordered matcher and exact-path work assertions across independent ignore, finding, and required-match sizes; verify tracker indices, first-match provenance, missing-identity fallback, and fail-closed waivers.

## 3. Verify measured effects and synchronize the capability

- [x] 3.1 Rerun the same before/after matrix including small and wildcard-heavy cases; update the evidence artifact with actual timing, allocation, retained-memory and work results, explain material variance, and verify the indexed effect contract.
- [x] 3.2 Synchronize the `ignore-matching` capability delta to the verified behavior and run `openspec validate index-exact-baseline-waiver-matching --type change --strict`.

## Workflow follow-up

- Archive the change after implementation, tests, and spec synchronization are complete.
- Run `openspec validate --all` after archive and inspect the synchronized spec and archived artifacts.
