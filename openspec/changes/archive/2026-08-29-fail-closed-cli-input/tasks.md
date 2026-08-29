## 1. Fail-closed CLI dispatch

- [x] 1.1 Reject parser-unmatched top-level and nested command tokens through the existing invalid-arguments diagnostic path before command invocation.
- [x] 1.2 Preserve declared positional arguments and valid root/subcommand help while enabling parser-owned unmatched-token errors.

## 2. Regression coverage and verification

- [x] 2.1 Add focused host and process-invocation regressions for unknown top-level and nested command tokens, invalid options, and successful explicit help.
- [x] 2.2 Run CLI-focused tests, formatter, and relevant OpenSpec validation.
