## 1. Evidence contract and decision model

- [ ] 1.1 Add typed #493 workflow, process/projection, revision-role, resource-measurement, and prepared-effect records that compose `benchmark-evidence/v1`; verify deterministic serialization and break-even/effect calculations with focused NUnit tests.
- [ ] 1.2 Add contract fixtures for required command-family attribution, candidate/base separation, cache-avoidance accounting, outcome A/B/C routing, and privacy-safe identities; verify they remain in the normal deterministic test bucket.

## 2. Consumer-shaped measurement harness

- [ ] 2.1 Extend the synthetic adoption fixture support with real-MSBuild/receipt-backed and prebuilt/staged-assembly candidate setup, exact artifact verification, and explicit preparation-boundary metadata; verify equivalent candidate inputs produce matching canonical identities.
- [ ] 2.2 Implement the explicitly invoked multi-command harness for strict, audit, no-new-debt, Architecture Health, current-side change snapshot, and applicable optional projections; verify every measured child process retains its raw `analysis-profile/v1` payload, counters, command identity, and exit/publication status.
- [ ] 2.3 Implement the one-process multi-projection comparison over one immutable `ArchitectureAnalysisSnapshot`, record shared versus process-bound projections, and measure independent one-shot and bounded-parallel variants over the same candidate state; verify canonical findings, identities, ordering, and exit semantics are equivalent.
- [ ] 2.4 Add separate base/reference revision execution and routing so base change snapshots are not counted as candidate reusable work; verify the expected-effect model excludes base-state preparation from candidate savings.

## 3. Public-safe evidence and documentation

- [ ] 3.1 Add the checked-in synthetic/anonymized #493 machine-readable evidence artifact with raw profiles or explicit unavailable measurements, workload dimensions, command mix, cache modes, canonical digests, and A/B/C decision; verify it validates against the benchmark evidence schema and contains no private adopter identifiers.
- [ ] 3.2 Add concise internal evidence documentation describing the two adopter archetypes, phase/counter boundaries, one-process alternative, break-even calculation, expected-effect contract, routing, and refresh command; verify docs lint and links pass.
- [ ] 3.3 Update the benchmark foundation README/profile dictionary to document the #493 reuse contract and distinguish candidate from base/reference measurements; verify terminology matches the archived OpenSpec requirement.

## 4. Validation and completion

- [ ] 4.1 Run the focused benchmark/evidence NUnit tests, formatter, directly implicated benchmark/schema checks, and `openspec validate --all`; inspect the diff for unrelated files and stale generated artifacts.
- [ ] 4.2 Synchronize and archive the OpenSpec change, rerun `openspec validate --all`, and verify the main `large-solution-benchmarking` spec contains the new requirements without delta headers.
