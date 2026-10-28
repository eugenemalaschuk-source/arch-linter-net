## 1. Badge operation isolation

- [x] 1.1 Extract preflight and runtime integration execution and failure handling into read-only collaborators and retain explicit CLI dispatch and publisher failure policy.
- [x] 1.2 Extend behavioral and boundary tests for read-only success, unavailable decisions, handled failures, and the real action-root entry point; retain publish/renew regression coverage.

## 2. Approved publisher acquisition

- [x] 2.1 Implement one command using the existing approved workflow/action pin authority and replace the six release-job fetch copies, preserving each verification step.
- [x] 2.2 Add acquisition, failure, shallow checkout and workflow-wiring regression coverage without weakening existing source and candidate verification.

## 3. Integration and validation

- [x] 3.1 Run the focused badge and release tooling suites, workflow lint, formatter, and applicable release-authority checks; repair relevant failures.
- [x] 3.2 Compare implemented behavior with both delta specs and synchronize/archive the completed change.
