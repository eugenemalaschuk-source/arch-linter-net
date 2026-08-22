## Why

Teams adopting architecture contracts need CI to reject newly introduced architecture debt without pretending that reviewed legacy debt has disappeared. The existing exact baseline lifecycle and policy-weakening guardrail provide the two authoritative inputs, but no single fail-closed workflow currently composes them for CI.

## What Changes

- Add an explicit `gate` CLI/Core/Testing workflow that evaluates complete current architecture results, compares persistent findings to an explicit reviewed baseline, and optionally consumes explicit base/current policy-context artifacts for policy weakening.
- Preserve strict/audit evaluation, baseline lifecycle statuses, and policy-weakening findings as separate typed sections of one normalized gate result; do not introduce a `ratchet` validation mode or write/update baseline files.
- Add deterministic Human, JSON, and SARIF projections plus exit semantics that fail for new debt, untrusted baseline comparison state, or error-severity policy weakening.
- Document CI invocation, review responsibilities, output interpretation, fail-closed limits, and the distinction between persistent debt and change-time weakening evidence.

## Capabilities

### New Capabilities

- `architecture-debt-gate`: deterministic, fail-closed composition of current architecture evaluation, exact persistent-debt comparison, and optional policy-weakening guardrails.

### Modified Capabilities

- `cli-command-dispatch`: expose the composed gate through a dedicated instance-based command module and handler.
- `test-adapter`: expose the typed gate request and result through the NUnit-friendly adapter.
- `sarif-diagnostics-output`: project normalized gate sections without losing the independent persistent-debt and policy-weakening semantics.
- `docs-site`: document the public new-debt CI workflow and its explicit boundaries.

## Impact

Affected areas are `ArchLinterNet.Core` validation/formatting composition, the CLI command modules and runtime seam, the Testing adapter, output formatters, NUnit fixtures, and MkDocs CI/migration/output guidance. Existing `validate`, `baseline`, and `policy weakening` behavior remains unchanged when `gate` is not requested.
