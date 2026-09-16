## 1. Implementation

- [x] 1.1 Add committed configuration and independent read-only workflow.
- [x] 1.2 Add bounded scan/evidence tooling and opt-in inspection/rerun validation.
- [x] 1.3 Add focused regression tests and internal adoption runbook.
- [x] 1.4 Checkpoint every completed burn-in phase and integrate Qodana tests into existing Python tooling coverage.

## 2. Validation

- [x] 2.1 Run focused runner/workflow tests and the existing Python tooling coverage suite: 36 and 546 tests passed in run 35073258396.
- [x] 2.2 Run implicated formatters, actionlint, zizmor and OpenSpec validation on implementation SHA f8d6b4ebf04b5cf4b66239e3e4f6a176f9e47ef8; archive is deliberately a separate unfinished task.
- [x] 2.3 Obtain actual canonical scanner, cold/warm and positive/corrected-negative inspection evidence: run 35072109010, artifact 10437360502.
- [x] 2.4 Record initial rule/severity/path inventory and bounded sample triage without suppressing findings or claiming full review.
- [ ] 2.5 Record actual fork execution, representative independent reruns and job/billing evidence.
      (Partial: an independent local cold scan of `f8d6b4eb` on a different machine, OS and
      independently pulled image reproduced the identical fingerprint — see the adoption doc's
      "Independent cross-machine repeatability" section. Fork execution and Actions job/billing
      minutes are still unrecorded.)
- [ ] 2.6 Complete unique-signal/noise/debt review versus existing gates and record a maintainer-reviewed PROMOTE or KEEP ADVISORY decision.
- [ ] 2.7 Revalidate final documentation, synchronize specifications and archive only after the remaining adoption acceptance is actually complete.
