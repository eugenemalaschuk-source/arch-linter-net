> **Historical / superseded.** This #945 design was implemented by #946 and later superseded by the no-App bootstrap handoff in #963/#964. It is preserved only as historical evidence. Current product authority is the canonical spec under `openspec/specs/`; do not implement the GitHub App writer model from this archived change.

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
- [x] 3.2 Historical implementation merged via #946 for #945; subsequently
  superseded by #963/#964.
