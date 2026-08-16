## 1. Strict policy workflow

- [x] 1.1 Add the dedicated pull-request and `main` architecture-policy workflow that runs `make lint-architecture` with read-only permissions.
- [x] 1.2 Add a focused structural test and Make target proving the workflow trigger, authoritative gate, and publication-free permissions.

## 2. Public trust documentation

- [x] 2.1 Add the dynamic architecture-policy workflow badge to the README and distinguish it from architecture coverage and test coverage.
- [x] 2.2 Document local validation, default-branch refresh behavior, and troubleshooting in the CI integration guide.

## 3. Verification and synchronization

- [x] 3.1 Run the focused workflow-badge test, formatter, strict self-policy/coverage validation, and relevant documentation checks.
- [x] 3.2 Synchronize the implementation with the OpenSpec artifacts, archive the completed change, and validate all specifications.
