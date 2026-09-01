## 1. Runner-temp trust boundary

- [x] 1.1 Add a symlink-safe, environment-anchored runner-temp path validator that fails closed for
  absent or mismatched contexts.
- [x] 1.2 Route only `verify-sonar`'s read-only scanner-log argument through the new validator and
  retain `_safe_path` for repository-controlled inputs.

## 2. Regression coverage

- [x] 2.1 Add focused tests for a valid synthetic runner-temp scanner log and for arbitrary or
  mismatched external scanner-log paths.
- [x] 2.2 Run focused release-tooling tests, formatter, OpenSpec validation, and the affected
  workflow/tooling lint checks.
