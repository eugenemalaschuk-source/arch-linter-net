## Why

`SarifEvidenceReader` is a six-file public partial aggregate that mixes repository-local artifact acquisition with SARIF document parsing, context validation, and source-diagnostic projection. Those responsibilities need independent internal ownership while preserving the reader as the one caller-facing trust authority.

## What Changes

- Reduce `SarifEvidenceReader` to one non-partial public facade while retaining its existing constructor and `Read(...)` signature.
- Extract internal repository-local artifact resolution, containment/reparse validation, regular-file opening, bounded byte reading, and SHA-256 provenance acquisition.
- Extract internal SARIF 2.1 parsing, selected-run validation, producer/context binding, and source-diagnostic projection.
- Remove the exact reviewed `SarifEvidenceReader` declaration-count exception from the self-policy without creating another production partial aggregate.

## Capabilities

No externally observable capability or contract changes. This is a behavior-preserving internal refactor, so this scoped change intentionally skips delta specs; the active `decompose-god-classes` change remains the source of truth for the aggregate-cleanup architecture policy.

## Impact

Core Execution evidence reading internals, focused Core trust-binding/source-projection tests, the reviewed self-policy declaration-count exception, and the active architecture-cleanup change evidence. No public API, policy-schema, CLI, cache, or consumer behavior change is intended.
