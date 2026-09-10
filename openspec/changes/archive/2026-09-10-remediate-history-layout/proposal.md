## Why

The frozen post-v0.8 self-architecture audit identifies two History exception classes and one
History provider interface outside the repository's recursive `Exceptions` and `Abstractions`
layout conventions. This focused remediation removes those exact audit findings without weakening
policy or changing History behavior.

## What Changes

- Relocate `HistoryDotNetEnrichmentUnavailableException` and `CanonicalJsonUnicodeException` to
  nested History `Exceptions` directories while preserving their namespaces and behavior.
- Relocate `IHistoryDotNetFactProvider` to `History/Enrichment/Abstractions`; keep its adjacent
  materialization model outside that abstractions directory.
- Add self-policy audit regressions that prove misplaced History exception and interface fixtures
  are still diagnosed.
- Record the exact frozen before-to-after identity/path mapping in implementation evidence.

## Capabilities

No spec-level behavior changes: this is a source-layout-only conformance repair. The existing
`layout-convention-contracts` behavior and policy remain unchanged.

## Impact

- Affected source: `src/ArchLinterNet.Core/History/**`.
- Affected focused tests: `SelfPolicyNegativeRegressionTests` and its mutation helper.
- No public API, namespace, schema, dependency, policy, baseline, or output changes.
