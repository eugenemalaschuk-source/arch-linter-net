## 1. Deterministic combined-mode evidence

- [x] 1.1 Extend focused snapshot/profile tests to prove `--ensure-built` preparation and one immutable analysis snapshot serve both strict and audit without changing per-mode results or aggregate exit behavior.
- [x] 1.2 Verify that multi-sink combined reporting changes only rendering/output evidence and does not re-evaluate analysis.

## 2. Canonical CI and adoption guidance

- [x] 2.1 Update CI, adoption, upgrade, reference-entrypoint, and output-format guidance with the combined strict-and-audit workflow for one required build state.
- [x] 2.2 Preserve and explain the separate strict-gating/non-blocking-audit workflow and the lack of cross-process prepared-state reuse.

## 3. Verification and specification synchronization

- [x] 3.1 Run focused tests, formatting, documentation linting, and strict OpenSpec validation; fix related failures.
- [x] 3.2 Synchronize the implemented behavior with the OpenSpec change and archive it before opening the pull request.
