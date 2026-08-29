## Context

The policy loader currently has no external-evidence configuration and Core has no SARIF
ingestion. The applicability model already reserves typed reasons for malformed and wrong-context
external evidence, while #521 and #522 intentionally own selection and normalized finding output.
See proposal.md and the `external-sarif-evidence` delta for the product contract.

## Goals / Non-Goals

**Goals:**

- Provide a reusable Core boundary that reads one declared local SARIF artifact, proves or rejects
  its identity and analysis context, and retains bounded trust provenance for downstream work.
- Make invalid policy declarations fail during policy loading and artifact trust failures return
  typed results rather than masquerading as zero diagnostics.
- Reuse the existing policy-import, Core.Model, Core.Execution, Core.IO, and applicability-reason
  seams without adding a second evidence or finding pipeline.

**Non-Goals:**

- Selecting SARIF results, mapping severities, deduplicating fingerprints, or creating normalized
  architecture findings (#521/#522).
- Running analyzers, builds, restores, service APIs, remote URLs, timestamps, or job-name-based
  freshness inference.
- Wiring external evidence into the CLI assessment execution before a consuming external-diagnostic
  family exists.

## Decisions

### Policy declaration stays a root-level, typed requirement collection

`external_evidence` will be a root policy collection. Each entry provides a unique logical `id`,
the `sarif` format, `required` flag, expected tool name (optional version), expected SARIF
automation/run id, and booleans for repository, revision, and scope binding. The document model,
import composition allow-list, raw YAML shape validator, and post-deserialization validator will
all be updated together. This allows #521 to extend the same entry with filters rather than adding
a parallel config block.

The policy declares *which bindings are required*, while the invocation supplies the current
assessment context and the producer/CI context attached to the concrete artifact. A policy never
hard-codes a changing commit SHA. This is preferred to filename, mtime, or workflow identity
heuristics, none of which prove artifact provenance.

### A reader returns a closed trust result instead of diagnostics

`Core.Execution` will own a reader that accepts a typed requirement, a repository root, an
artifact reference, an expected assessment context, and explicit limits. Its result records one
closed trust status, actionable detail, normalized repository-relative path, artifact SHA-256 when
bytes were available, selected tool/run facts, result count, and resolved analysis context. Normal
trust failures are values, not exceptions; invalid method arguments remain programming errors.

This isolates physical input handling in the existing execution seam and lets #521 consume an
already-trusted result. It avoids a premature external-diagnostic contract handler or output
envelope. The reader will use the existing `IArchitectureFileSystem` seam, extended with bounded
stream opening, so focused tests can control I/O without analyzer or network dependencies.

### Standard SARIF fields and explicit producer context are merged conservatively

The reader accepts SARIF 2.1.0 only. It selects exactly one run by expected
`tool.driver.name` and `automationDetails.id`, verifies the optional configured driver version,
and requires explicit successful invocation metadata. Repository/revision may come from standard
`versionControlProvenance`; scope and logical evidence key may come from explicit producer/CI
context. For each binding field, two supplied values must agree; absence is accepted only when
that binding is not required. Any conflict or required absence is unassessable.

This is vendor-neutral and lets a CI producer manifest supply fields SARIF does not standardize,
without trusting a vendor API. A strict exact comparison is deliberate: path, repository, revision,
scope, key, tool, and run identities are opaque identifiers rather than text to normalize or guess.

### Resource limits precede JSON traversal and trust hashing is byte-based

The reader resolves a regular path under the repository root, rejects reparse-point escape, then
streams no more than a configured maximum into a bounded buffer while computing lowercase SHA-256.
It parses the exact consumed bytes with strict JSON settings, enforces SARIF version/shape, caps
run count, and caps result count for the selected run. Results are not projected in this issue;
only their count is established. This preserves a deterministic content hash even when an artifact
is later rejected for malformed structure or incorrect context.

Streaming bounded bytes is preferred to `ReadAllText` because it gives an enforceable byte ceiling
and hashes exactly the consumed artifact. Parsing each reader call rather than caching documents
prevents one invocation's mutable/undisposed JSON state from leaking into another assessment.

## Risks / Trade-offs

- [A producer has no usable revision metadata] → callers provide explicit producer/CI context;
  policies requiring revision binding remain unassessable if it is absent.
- [SARIF permits broad optional structures] → v1 validates only the bounded fields it consumes and
  rejects ambiguous matching runs instead of guessing across them.
- [Extending the file-system seam expands reviewed API] → use a default-compatible member, real
  implementation, focused fake coverage, and the explicit public-API review lifecycle.
- [A raw JSON artifact is near the size limit] → limits apply before JSON parse and selected result
  count is checked before any future selection work.

## Migration Plan

This is opt-in: policies without `external_evidence` retain their current loading and validation
behavior. A new policy can declare requirements and call the reader through a future consuming
family. Removing the new configuration or not supplying optional evidence returns to the existing
no-external-evidence behavior; no persisted schema or remote state is migrated.
