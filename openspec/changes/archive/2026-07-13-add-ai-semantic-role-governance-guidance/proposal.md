## Why

AI-assisted development can add plausible namespaces, shared helpers, and cross-context references faster than existing architecture guidance can be reviewed. ArchLinterNet needs an explicit, explainable governance workflow that helps agents classify new code, interpret semantic-role diagnostics, and preserve narrow policy boundaries without weakening static-analysis guarantees.

## What Changes

- Add public AI-first guidance for semantic role discovery and contextual contracts.
- Document an agent workflow for new features, bounded contexts, shared-kernel exceptions, and legacy migration.
- Define actionable diagnostic fields and feedback-loop guidance for human-readable, JSON, CI, and AI-agent output.
- Document fail-closed handling for ambiguous or uncovered roles, safe narrow exceptions, and staged audit-to-strict adoption.
- Add Sales/Inventory/SharedKernel and Unity/client-style examples, including review guidance for multi-file AI changes.
- Explicitly warn against broad overrides, unrestricted regex, blanket ignores, and claims beyond static analysis.

## Capabilities

### New Capabilities

- `ai-semantic-role-governance-guidance`: AI-first documentation and examples for authoring, reviewing, and diagnosing semantic-role architecture policies.

### Modified Capabilities

- None. Existing semantic-role and AI policy requirements remain unchanged; this change documents their intended governance use.

## Impact

- Adds and updates public Markdown documentation and executable sample policies under `docs/` and `samples/`.
- Adds an OpenSpec capability specification; no production code, public API, runtime dependency, or schema behavior changes.
- Documentation validation and sample/schema validation become the primary verification activities.
