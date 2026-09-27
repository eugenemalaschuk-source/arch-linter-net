## 1. Align publisher validation

- [x] 1.1 Replace the publisher's stable/preview-only regular expression with the shared package-version parser while retaining canonical `v` prefix validation.
- [x] 1.2 Add publisher regression tests for `rc`, `alpha`, and build metadata tags and keep malformed identity rejection covered.

## 2. Synchronize contract and validate

- [x] 2.1 Add the publisher's accepted-version behavior to the manual release specification.
- [x] 2.2 Run focused and full tooling tests, format checks, and OpenSpec validation; record exact results.
