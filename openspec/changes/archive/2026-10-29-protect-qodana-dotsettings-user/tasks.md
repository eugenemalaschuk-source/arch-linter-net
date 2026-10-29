# Tasks

## 1. Protect ReSharper user-layer settings

- [x] 1.1 Protect nested `.DotSettings.user` basenames case-insensitively and verify positive, negative, and governance projection behavior with `python3 -m unittest tests.qodana.test_qodana_gate`.
- [x] 1.2 Update the Qodana adoption guide to document both protected ReSharper settings suffixes and verify the edited policy text.

## 2. Validate the archived contract

- [x] 2.1 Run the Qodana gate contract suite (81 tests), Python bytecode compilation, and `git diff --check` successfully.

## Workflow follow-up

- Archive the completed change before updating PR #1119, then run `openspec validate --all` to verify the main Qodana spec contains the updated requirement.
