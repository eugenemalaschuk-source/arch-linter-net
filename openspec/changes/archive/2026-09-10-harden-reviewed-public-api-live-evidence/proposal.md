# Harden reviewed public API live evidence

## Why

A reviewed approval must not let a hand-edited policy-context snapshot claim a public CLR API addition that the analysed assembly does not export. The current requirement describes a snapshot delta but does not require evidence from the live CLR surface.

## What changes

- Bind reviewed public API addition approval to a fresh, read-only capture of the current CLR public API surface.
- Require the current policy-context snapshot to exactly match that capture before suppressing its narrow impact-not-proven finding.
- Keep all other weakening findings fail-closed and visible.

## Impact

- Affected capability: `policy-weakening-guardrails`
- Affected code: policy weakening approval evaluator, debt-gate orchestration, policy CLI, tests, and CLI documentation
