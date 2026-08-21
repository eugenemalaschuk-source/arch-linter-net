## Why

AI coding agents need a compact, deterministic view of the architecture policy
before changing code. Today they must reconstruct that context from policy
files, imported fragments, documentation, and diagnostics, which makes common
architecture-drift mistakes more likely.

## What Changes

- Add a versioned Core policy-context export derived from the effective composed
  policy and its existing provenance model.
- Add `arch-linter-net policy context` with deterministic JSON and compact
  Markdown output, without invoking project or assembly analysis.
- Export declared contracts, layers and selectors, semantic-role mappings,
  metadata keys, contexts, coverage scopes, reviewed policy provenance, and
  bounded AI-safe guidance.
- Document the command and its static-analysis, non-authoritative boundary.

## Capabilities

### New Capabilities

- `policy-context-export`: Exports a safe, deterministic architecture-policy
  context for AI coding agents from an effective policy.

### Modified Capabilities

- `cli-command-dispatch`: Adds the `policy context` command below the existing
  executable and policy command family.
- `policy-import-composition`: Makes the context export another consumer of
  the existing composed-policy and provenance facts.

## Impact

Affected areas are the public Core composition API, CLI policy command module
and runtime boundary, reviewed public API snapshot, CLI/Core tests, command
documentation, and the related OpenSpec contracts. It adds no dependencies,
schema fields, policy parser, executable, or validation behavior changes.
