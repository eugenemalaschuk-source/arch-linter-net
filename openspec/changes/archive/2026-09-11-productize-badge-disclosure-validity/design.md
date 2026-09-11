## Context

See `proposal.md`. The current `badge architecture-health` path maps a complete
`architecture-health/v1` report into the existing four-fact Shields payload.
The merged #826 contract has already fixed the public profile names, byte
representation, color dictionary, maximum payload size, and Relay lease rules,
but validity/reuse evidence and a product-owned profile boundary are absent.

## Goals / Non-Goals

**Goals:**

- Derive one deterministic, finite reuse horizon from existing canonical
  evaluation, waiver, inventory, and external-evidence authorities.
- Make the CLI the one projector and validator for the two approved public
  profiles, including exact-byte/digest behavior and actionable failure output.
- Make the contract assets package-visible and prove them from a packed CLI.

**Non-Goals:**

- Relay runtime, OIDC validation, storage, provider workflows, rendering, or
  README/deployment changes owned by downstream issues.
- A second Architecture Health evaluator, a changed Health identity, arbitrary
  payload sanitization, or new public provenance fields.

## Decisions

### Core owns a typed publication-evidence receipt

Add a narrow Core model/projector adjacent to the existing Architecture Health
report-evidence writer. It consumes the canonical report/evaluation receipts and
returns either a finite UTC reuse horizon with typed reasons or an explicit
unassessable result. It computes the minimum applicable boundary and never reads
`DateTime.Now`; the caller supplies the evaluated-at context. This avoids making
the CLI or Relay reproduce waiver/external-evidence business rules.

Alternative: have the Relay interpret Health JSON. Rejected because it makes a
transport a second semantic authority and cannot safely resolve absent facts.

### CLI owns closed profile serialization and validation

Retain the existing unprofiled command behavior. Add a profile-aware path that
uses a small typed representation serializer with `System.Text.Json` default
escaping and explicitly checks raw UTF-8 input: object shape, property order,
duplicate keys, exact names, dictionary membership, count bounds, timestamps,
size, and SHA-256. A strict profile rejects rather than normalizes bytes; the
output writer never reserializes accepted input after digest verification.

Alternative: JSON Schema at runtime. Rejected because the package needs
cross-language schema assets but exact ordering/escaping and duplicate-key
validation require byte-aware product logic.

### Migration keeps legacy snapshot output distinct

The existing raw/unprofiled health badge remains the legacy snapshot contract.
The strict profile command requires publication evidence containing a complete
finite horizon; legacy report evidence therefore receives a clear upgrade
diagnostic in strict mode, not an invented validity time.

### Work slices and exclusive boundaries

1. Core evidence models/projector and Core tests: `src/ArchLinterNet.Core/**`
   and `tests/ArchLinterNet.Core.Tests/**` limited to publication evidence.
2. CLI profile serializer/validator/commands and CLI tests: Badge command files,
   package metadata/assets, and `tests/ArchLinterNet.Cli.Tests/**`.
3. Contract schemas/fixtures and packed consumer test: the existing internal
   fixture directory, schema/package content assertions, and isolated package
   integration tests.

The CLI slice depends on the Core receipt shape. The fixture and packed-consumer
slice follows the settled profile output, so work runs serially rather than
concurrently across tightly coupled command and asset files.

## Risks / Trade-offs

- [Existing receipts lack some validity facts] → return an explicit upgrade
  diagnostic; never infer a horizon.
- [Public API drift] → keep new Core types minimal, update only via the reviewed
  public API lifecycle, and run its read-only check before the PR.
- [Byte-validation implementation errors] → use source-projector golden vectors
  plus malicious duplicate/escape/extra-field/oversize corpus tests.
- [Pack layout drift] → test a freshly packed CLI artifact from an isolated
  directory and assert schema/fixture discovery there.

## Migration Plan

1. Existing consumers retain unprofiled snapshot output.
2. Strict consumers select an explicit profile and provide complete publication
   evidence; incomplete legacy evidence fails with an upgrade diagnostic.
3. Downstream Relay work consumes the new exact bytes and receipt without
   re-deriving any domain facts.
