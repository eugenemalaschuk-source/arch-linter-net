## Why

`ArchitectureValidationBuilder.WithPolicyWeakeningContexts(...).EvaluateDebtGate(...)` is a documented in-process consumer of the same versioned policy-context JSON that the CLI exports and compares. Issue #788 reports that the packaged Testing path rejects a schema-5 artifact that the packaged CLI accepts, so the parity contract needs one explicit import authority and a regression that exercises the real serialized artifact through the Testing gate.

## What Changes

- Make policy-context JSON import a Core-owned shared operation used consistently by the CLI and Testing debt-gate entry points.
- Preserve fail-closed schema, kind, identity, completeness, and effective-policy evidence validation while accepting the current CLI-exported schema-5 document through the Testing API.
- Add a regression covering CLI-shaped schema-5 JSON, the Testing builder, strict-to-audit weakening, and unchanged-context control behavior.
- Document the Testing/CLI artifact-parity guarantee and the requirement to regenerate context artifacts with a compatible release.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `openspec/specs/test-adapter/`: require `WithPolicyWeakeningContexts(...).EvaluateDebtGate(...)` to consume the same versioned policy-context JSON contract emitted by the CLI.

## Impact

The affected implementation is the Core policy-context import/validation seam and its CLI and Testing callers, plus the Testing and policy-context documentation and focused NUnit coverage. No public policy schema, weakening semantics, baseline lifecycle, or architecture dependency boundary changes are intended.
