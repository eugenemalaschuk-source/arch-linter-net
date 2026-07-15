## Why

Issue #324 fixed the CEL language profile and public engine boundary, but the
product still lacks a normative user-facing policy model for where expressions
can appear, what facts they can inspect, and how failures behave. Without that
design, #163 could drift into ad hoc YAML parsing, hidden expression support,
or policy-weakening runtime behavior.

## What Changes

- Add a new `cel-policy-model` capability spec that defines the CEL-compatible
  policy expression surface for ArchLinterNet.
- Define the explicit YAML locations that may eventually accept expressions,
  and the much larger set of fields where expressions remain forbidden.
- Define the typed CEL context objects and fact catalog exposed to
  selector-backed layers and contextual contracts.
- Define compile-time and evaluation-time failure behavior, strict/audit
  interaction, explainability, JSON/baseline/coverage implications, and
  backward-compatibility rules.
- Publish a durable internal design blueprint so #163 can implement the model
  using only the public `ArchLinterNet.CEL` API and without revisiting product
  semantics.

## Capabilities

### New Capabilities
- `cel-policy-model`: Normative design for explicit CEL-backed policy
  predicates, typed architecture fact contexts, and fail-closed behavior.

### Modified Capabilities

None.

## Impact

- OpenSpec: new capability spec, proposal, design, and tasks for the policy
  expression model.
- Documentation: new internal CEL policy model blueprint plus supporting docs
  updates that keep the feature clearly unimplemented until #163 lands.
- Future implementation scope: `src/ArchLinterNet.Core/Contracts`,
  policy-loading validation, JSON schema/docs, explain output, coverage/reporting,
  and the public authoring guidance for expression-bearing policies.
