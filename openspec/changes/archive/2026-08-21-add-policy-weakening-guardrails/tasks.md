## 1. Policy context and guardrail contracts

- [x] 1.1 Add schema-validated `analysis.policy_weakening` configuration and project it through deterministic JSON/Markdown policy context.
- [x] 1.2 Add public Core policy-weakening result, evidence, membership, validation, and formatter models with stable identity and fail-closed artifact parsing.

## 2. Deterministic comparison

- [x] 2.1 Implement strict removal/downgrade and imported-provenance comparisons.
- [x] 2.2 Implement typed source-set, subtraction, explicit permission/prohibition, and bounded exception comparisons.
- [x] 2.3 Implement bounded selector/public-surface comparison and exact subjects only for context-bound complete membership evidence.

## 3. Command and documentation

- [x] 3.1 Add the instance-based `policy weakening` CLI command with human, JSON, SARIF, severity, and fail-closed exit behavior.
- [x] 3.2 Update output-format, CLI, policy authoring, review, and capability guidance with separate-state artifact examples and limits.

## 4. Verification and OpenSpec lifecycle

- [x] 4.1 Add focused Core and CLI tests covering semantic, bounded, invalid-input, provenance, severity, and format-parity cases.
- [x] 4.2 Update reviewed public API evidence and relevant self-policy rules.
- [x] 4.3 Run formatter, focused test suites, architecture lint, strict OpenSpec validation, synchronize actual behavior, and archive the change.
