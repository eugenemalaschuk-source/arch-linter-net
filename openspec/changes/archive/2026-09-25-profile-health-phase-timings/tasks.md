## 1. Time the Health analysis

- [x] 1.1 Add timing-aware internal Core Health orchestration and measure the total interval, mode evaluations, external-evidence binding, debt-gate comparison, and Health projection using the existing `ValidationTiming`; verify the timed and untimed outcomes remain equivalent.
- [x] 1.2 Thread the same optional timing through the CLI runtime for ordinary Health and caller-owned snapshot paths; verify `--change-snapshot` still materializes one snapshot and records its phases.

## 2. Publish and document the profile

- [x] 2.1 Capture Health process measurements when `--profile` is requested and pass timing/measurements to the shared profile publisher; verify the profile contains `total`, preparation/evaluation phases, and snapshot counters while Health output and exit categories match the unprofiled run.
- [x] 2.2 Document Health-specific phase names and their boundaries in the analysis-profile dictionary; verify every emitted phase has a dictionary entry.
- [x] 2.3 Retain the Health profile from the existing CI projection as a separate optional artifact; verify the workflow's Health gate and report destinations remain unchanged.

## 3. Refresh attribution evidence

- [x] 3.1 Rerun the public-safe synthetic independent-process harness on the stabilized tree, validate each sample's status/exit/output and canonical result digest, and refresh its evidence report without adopter identifiers.
- [x] 3.2 Reconcile the attribution report with the post-normalization decision recorded in #991, state the measured boundary and remaining attribution limit, and route confirmed repeated preparation to its existing owner; verify no duplicate optimization task or private evidence is introduced.
