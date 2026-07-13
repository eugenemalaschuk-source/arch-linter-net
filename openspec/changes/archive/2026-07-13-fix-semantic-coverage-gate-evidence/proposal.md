# Fix semantic coverage gate evidence

## Why

Strict semantic coverage reports stale selectors and classification diagnostics only in summaries, allowing false-green validation. Semantic evidence also omits its governance mechanism and conflict provenance, while `metadata: null` can throw at runtime.

## What Changes

- Include stale selectors, conflicts, and metadata failures in semantic coverage findings.
- Reject null semantic exclusion metadata and preserve complete evidence with deterministic formatting.

## Impact

- Affected capabilities: `architecture-coverage-reporting`, `yaml-contract-loading`
