## 1. OpenSpec And Workflow Design

- [x] 1.1 Add the `github-actions-ci` spec delta for SonarCloud branch analysis, pull-request quality gating, and README badge expectations.
- [x] 1.2 Document the implementation approach and operational assumptions in the change design.

## 2. CI And Coverage Implementation

- [x] 2.1 Update the CI workflow to install and run SonarScanner for .NET on `main` pushes and trusted `pull_request` runs.
- [x] 2.2 Make the workflow wait for the SonarCloud quality gate, fail closed for missing trusted-run configuration, and publish a reviewer-visible SonarCloud PR-analysis link.
- [x] 2.3 Update coverage collection so the existing test-coverage path emits both Codecov-compatible and SonarCloud-compatible reports from the same run.

## 3. Documentation And Validation

- [x] 3.1 Update the README badge section to add live SonarCloud quality badges while keeping the Codecov badge.
- [x] 3.2 Update the CI integration guide with required secrets/variables, PR decoration behavior, automatic-analysis caveats, and the recommended required-check guidance.
- [x] 3.3 Run formatting, acceptance validation, OpenSpec validation/archive, and record the final PR summary inputs.
