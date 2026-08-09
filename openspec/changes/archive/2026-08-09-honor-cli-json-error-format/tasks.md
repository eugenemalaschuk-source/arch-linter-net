## 1. Error-output contract

- [x] 1.1 Audit every command and subcommand that accepts `--format json` for owned early error returns.
- [x] 1.2 Implement the shared versioned JSON error envelope, including policy and build-state diagnostic details when available.
- [x] 1.3 Route the previously unstructured baseline and public-API error paths through the shared formatter, while auditing validation, policy-check, graph, and explain to retain their existing structured JSON output without changing human output or exit codes.

## 2. Regression coverage

- [x] 2.1 Add parsed JSON configuration-error tests for the affected command families.
- [x] 2.2 Add parsed JSON build-state/preflight-error tests for commands that expose those paths.
- [x] 2.3 Inspect remaining `--format json` error returns and add focused tests for any uncovered owned path.

## 3. Verification and specification synchronization

- [x] 3.1 Update output-format documentation with the error-envelope guarantee.
- [x] 3.2 Run focused CLI tests, formatting, and the full acceptance suite; fix issue-related failures.
- [x] 3.3 Synchronize and validate OpenSpec artifacts, archive the change, and inspect the generated main specs.
