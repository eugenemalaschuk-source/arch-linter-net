## 1. Bounded JWKS refresh implementation

- [x] 1.1 Replace per-miss fixed-endpoint refreshes with one bounded cache entry that records the validated key set, last attempt time, and one in-flight refresh; verify typecheck and the existing security seam still enforces the fixed GitHub URL.
- [x] 1.2 Apply the short refresh protection window to successful and failed attempts, preserve cached-key hits, and verify unknown keys remain fail-closed while later misses can refresh for provider rotation.

## 2. Regression coverage

- [x] 2.1 Add focused Relay tests proving cached valid keys do not refetch and repeated/concurrent unknown key IDs cause a bounded, coalesced number of provider calls.
- [x] 2.2 Add rotation, provider-outage/no-write, and many-distinct-invalid-key coverage; verify the existing issuer, audience, algorithm, workflow-pin, replay, and lifecycle suites remain green.

## 3. Contract and validation closure

- [x] 3.1 Synchronize the archived Relay runtime and contract specs, ADR, and conformance-fixture documentation with the implemented cooldown, single-flight, rotation, outage, and bounded-state guarantees; verify `openspec validate --all`.
- [x] 3.2 Run focused Relay tests, Relay typecheck, formatter/lint checks required by the repository, inspect the diff, and record exact results for the pull request.
