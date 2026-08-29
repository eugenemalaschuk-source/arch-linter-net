## 1. Fail-closed CLI dispatch

- [x] 1.1 Parse the complete argument vector before performing legacy root help or version rendering.
- [x] 1.2 Retain legacy root help/version output only after successful parsing.

## 2. Regression coverage and verification

- [x] 2.1 Add host-level coverage for leading help/version plus invalid input and standalone valid help/version.
- [x] 2.2 Add integration coverage for leading help/version plus unknown command and option input.
- [x] 2.3 Run focused CLI tests, formatting, lint, and strict OpenSpec validation.
