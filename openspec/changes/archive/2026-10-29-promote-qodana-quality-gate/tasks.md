# Tasks

## 1. Establish and remediate current quality debt

- [x] 1.1 Capture exact-revision Sonar and Qodana inventories for the current `main` SHA, including severities, paths, rule IDs, counts and report identities; verify the artifacts match that SHA.
- [ ] 1.2 Review and fix all current Sonar Blocker/Critical/Major issues and lower-severity findings that affect maintainability or code quality; verify the final Sonar analysis has no unresolved in-scope findings.
- [ ] 1.3 Review and fix all current Qodana High/Critical findings and lower-severity maintainability findings; verify with a fresh scan that no in-scope high-severity or selected maintainability finding remains.

## 2. Implement trusted Qodana differential reporting

- [x] 2.1 Keep the scanner read-only and tokenless, preserve bounded SARIF/log evidence on success and failure, and bind evidence to the exact run, attempt, repository, PR, head, base, image and profile; verify workflow contract checks cover each identity field.
- [x] 2.2 Add a trusted `workflow_run` publisher that validates exact-run artifacts and GitHub PR state, recomputes the verdict from raw SARIF and the base-revision baseline, and reports missing, malformed, stale or unsuccessful evidence as failed/unassessable; verify adversarial evidence fixtures are rejected.
- [x] 2.3 Generate a reviewed baseline from the exact current-main Qodana report, keep High/Critical findings out of the accepted baseline, block every new Moderate, and accept baseline refreshes only in isolated baseline-only PRs matching the successful scan of unchanged base source; verify additions fail, removals pass, and candidate updates cannot approve their own findings.
- [x] 2.4 Publish an idempotent bounded PR comment with severity counts, new/remediated findings, rule IDs, paths/lines, scan identity and artifact links; verify stale-head and duplicate-comment cases cannot publish misleading results.
- [x] 2.5 Refresh Qodana workflow/report tests and adoption documentation, including current version pin, severity policy, trust boundary, artifact handling and rollout steps; verify the documentation matches workflow names and schemas.

## 3. Require Qodana in repository policy

- [x] 3.1 Add the trusted `qodana` status to the versioned `Main` ruleset payload and required-check inventory; verify the context matches the publisher's exact status name and preserves all existing rules.
- [ ] 3.2 Validate the complete workflow and gate on a PR head, then confirm the trusted status and report are bound to that exact head; record remaining rollout steps if the publisher is not yet available on default branch.

## Workflow follow-up

- Apply the reviewed required-check payload to the active `Main` ruleset after the publisher workflow is present on the default branch, then verify a missing or failing `qodana` status blocks merge.
- Archive this OpenSpec change and run `openspec validate --all` after implementation and rollout evidence are complete.
