## 1. Fail-closed canonical collection integrity

- [x] 1.1 Require a produced record for every expected membership and preserve deterministic duplicate expected/record provenance; verify focused applicability evaluator tests cover missing optional/not-applicable, duplicate records, and reversed duplicate expectations.

## 2. Host outcome safety

- [x] 2.1 Prevent a completion `pass` from returning exit `0` when ordinary validation failed; verify focused CLI tests cover `Passed=false + completion=Pass`.

## 3. Contract and PR validation

- [x] 3.1 Validate the follow-up OpenSpec delta and run focused Core/Testing/CLI tests, the Core public-API approval test, formatter, API, lint, and full OpenSpec validation; inspect the final diff before archiving.
