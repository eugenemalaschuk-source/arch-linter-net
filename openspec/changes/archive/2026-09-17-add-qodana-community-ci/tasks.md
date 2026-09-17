## 1. Implementation

- [x] 1.1 Add committed configuration and independent read-only workflow.
- [x] 1.2 Add bounded scan/evidence tooling and opt-in inspection/rerun validation.
- [x] 1.3 Add focused regression tests and internal adoption runbook.
- [x] 1.4 Checkpoint every completed burn-in phase and integrate Qodana tests into existing Python tooling coverage.

## 2. Validation

- [x] 2.1 Run focused runner/workflow tests and the existing Python tooling coverage suite: 36 and 546 tests passed in run 35073258396.
- [x] 2.2 Run implicated formatters, actionlint, zizmor and OpenSpec validation on implementation SHA f8d6b4ebf04b5cf4b66239e3e4f6a176f9e47ef8.
- [x] 2.3 Obtain actual canonical scanner, cold/warm and positive/corrected-negative inspection evidence: run 35072109010, artifact 10437360502.
- [x] 2.4 Record initial rule/severity/path inventory and bounded sample triage without suppressing findings or claiming full review.
- [x] 2.5 Verify fork/untrusted behavior and record representative independent reruns and job/billing evidence.
      Successful normal PR runs 35153454405, 35181806506 and 35182465418 completed in 416 s,
      418 s and 426 s respectively; GitHub's Actions timing endpoint reported 0 ms billable Ubuntu
      usage for each public-repository run. Cross-machine repeatability was also recorded. At closure,
      repository metadata reports `pull_request_creation_policy: collaborators_only` and zero forks,
      so a literal non-collaborator fork PR is not an available repository path. The security objective
      is verified structurally: Qodana uses ordinary `pull_request`, an ephemeral hosted runner,
      `contents: read`, `persist-credentials: false`, no repository/Qodana secrets and no PR/security-event
      write permissions. If the repository later changes PR creation policy to `all`, record a real
      untrusted-fork run before considering promotion.
- [x] 2.6 Complete unique-signal/noise/debt review versus existing gates and record a maintainer-reviewed PROMOTE or KEEP ADVISORY decision.
      Decision: **KEEP ADVISORY**. Fresh run 35182465418 produced 12,164 repository-wide
      diagnostics (10,143 notes, 2,020 warnings, 1 error); 8,147 are under tests and 3,885 under
      `src`. The 832 production-source warnings split into 488 style/mechanical, 243 dead/API-shape,
      83 nullable/dataflow, 17 possible-multiple-enumeration performance findings and 1 resource-safety
      finding. On the exact same head (`81bf2378c003d6d6f9a9ea13601d7670e603fb65`), SonarCloud's PR
      Quality Gate passed with 0 new issues. Qodana therefore provides genuinely independent signal,
      but the current repository-wide/no-baseline output mixes useful findings with substantial existing
      debt and is not suitable as a blocking regression gate without a separate reviewed baseline/new-code policy.
- [x] 2.7 Synchronize final documentation/specification and archive the completed change as
      `2026-09-17-add-qodana-community-ci`; authoritative final validation is PR CI.
