## Context

See [proposal.md](proposal.md). `SarifEvidenceReader` currently spreads a single public trust boundary across six handwritten partial declarations. The existing `IArchitectureEvidenceFileSystem` capability is the injected secure opening seam, and `SarifEvidenceReadResult` is consumed by the external-evidence binder, selector, federation, reporting, and CLI paths as the sole trust receipt.

The extraction must retain current order: input validation and missing-artifact handling; path safety and bounded acquisition; SARIF document/run validation; context binding; optional source projection; and one final immutable result. The reader currently checks cancellation at its read and document iteration boundaries; those checks are behavioral contract points.

## Goals / Non-Goals

**Goals:**

- Make artifact acquisition, document/run parsing, context handling, and source-diagnostic projection independently owned internal responsibilities.
- Preserve the public facade as the only component that composes trust status, provenance, authorization, and `SarifEvidenceReadResult`.
- Preserve byte-limit accounting, lowercase SHA-256 provenance, regular-file containment guarantees, status/details, deterministic ordering, and cancellation boundaries.
- Add direct internal-collaborator tests alongside the existing end-to-end reader tests.

**Non-Goals:**

- Alter public APIs, policy schema, SARIF accepted shapes, result/status semantics, CLI bindings, or cache behavior.
- Introduce an analyzer adapter, remote acquisition, a second trust implementation, or public extension seam.
- Reinterpret filesystem, run-selection, context, or source-projection rules while moving code.

## Decisions

### Retain one facade and return data from internal collaborators

`SarifEvidenceReader` remains one non-partial public declaration and retains its current constructor and `Read(...)` signature. It validates programmer errors, handles optional absence, calls acquisition and parsing collaborators, then constructs every `SarifEvidenceReadResult` and its provenance. Internal collaborators return narrowly typed success/failure facts; they do not publish results or become caller-facing trust authorities.

This avoids moving the public status/provenance decision boundary to a helper. Exposing separate public readers or giving consumers a parser result was rejected because it would invite competing trust decisions.

### Isolate repository-local acquisition behind an internal artifact reader

`SarifEvidenceArtifactReader` owns path normalization, root containment and reparse checks, the pre-open safety recheck, `IArchitectureEvidenceFileSystem` use, bounded streaming, partial-read failure facts, and SHA-256 calculation. Its internal outcome carries only normalized artifact identity, bytes, byte-count, digest, limit state, and acquisition failure classification.

The existing `IArchitectureEvidenceFileSystem` injection remains owned by the public facade and is passed to the artifact reader. Replacing it with direct `File` APIs or a new public interface was rejected because it would weaken the reviewed regular-file seam and change the API surface.

### Split SARIF parsing by stable responsibility while keeping its vocabulary local

`SarifEvidenceDocumentReader` owns JSON parsing, SARIF version/root validation, duplicate-property detection, matching-run selection, result bounds, and execution-state validation. `SarifEvidenceContextReader` owns SARIF/explicit producer context merge and binding validation. `SarifEvidenceSourceProjectionReader` owns driver-rule, artifact, source-location, and result projection validation; `SarifEvidenceSourceLocationReader` owns source location parsing mechanics.

They retain the existing private records and deterministic validation details beside the responsibility that uses them. The public facade composes their facts in the existing order and decides which ones become a result status/provenance. A generic JSON/SARIF framework or cross-format parser was rejected because this issue must preserve the intentionally strict SARIF 2.1 trust behavior without broadening a format abstraction.

### Preserve one bounded byte source for hash and trusted diagnostics

The document parser consumes the exact byte array supplied by the artifact reader. Source diagnostics are exposed only after all context checks succeed and only for a requirement with a diagnostic filter. Later selectors keep consuming `SarifEvidenceReadResult`; they never reopen the artifact or revalidate paths.

This keeps the existing hash/projection binding structural and prevents a time-of-check/time-of-use split.

### Remove the reviewed exception in the same change

The exact `SarifEvidenceReader` ignored violation is removed from `architecture/policy/audit-conventions.arch.yml` once the type has one declaration. The scoped change updates the active `decompose-god-classes` design/tasks with this completed extraction, but does not complete unrelated umbrella tasks.

## Risks / Trade-offs

- [Moving private code changes an observable status, detail, or validation order] → retain the facade’s ordering and use end-to-end regression tests as the authority, with focused collaborator tests for acquisition and parse/projection seams.
- [File-system extraction weakens containment, reparse, regular-file, or TOCTOU defenses] → keep `IArchitectureEvidenceFileSystem`, both safety checks, failure classification, and all path/bounds regression scenarios intact.
- [A new helper becomes another aggregate] → use one non-partial declaration per named responsibility and run the self-policy plus code-size gate.
- [Internal movement accidentally changes reviewed API] → run `make public-api-check`; no snapshot update is permitted for this refactor.

## Migration Plan

1. Introduce the internal acquisition and parser/projection collaborators, relocating existing private helper records/enums to their owning declarations.
2. Rewire the single public reader facade to compose existing facts and result construction in the existing order.
3. Add direct collaborator tests and retain/extend end-to-end reader regression coverage for paths, limits, statuses, context, zero-result evidence, and source diagnostics.
4. Delete the exact self-policy ignore; record the completed slice in the active umbrella change; run risk-appropriate Core, architecture, API, code-size, formatting, and OpenSpec checks.
5. Archive this scoped no-delta change after all tasks and synchronization evidence are complete; leave `decompose-god-classes` active.
