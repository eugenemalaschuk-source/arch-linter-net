## 1. Protected bootstrap implementation

- [x] 1.1 Add constrained GitHub App writer credential inputs and minimum
  permissions to the reusable bootstrap workflow.
- [x] 1.2 Replace direct base-ref push with deterministic branch creation and
  normal pull-request creation; refuse an existing branch.
- [x] 1.3 Keep writer authority isolated from OIDC inspection and managed-path
  staging.

## 2. Regression coverage and guidance

- [x] 2.1 Add static workflow regression coverage for direct-push prohibition,
  writer scope, deterministic branch, and PR handoff.
- [x] 2.2 Update protected-branch setup documentation.

## 3. Validation

- [x] 3.1 Run focused workflow/tooling tests, policy/format checks, and
  OpenSpec validation.
- [ ] 3.2 Open a focused PR for #945 and complete required upstream CI before
  merge.
