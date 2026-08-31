## Context

The existing external-evidence boundary already validates bounded local SARIF bytes (#520),
selects policy-authorized diagnostics without rechecking trust (#521), projects them through the
ordinary normalized finding/baseline/output paths (#522), and reports evidence completeness using
the shared applicability contract (#507). The focused tests prove those components independently.
Issue #523 closes the federation capability by exercising their composition with only synthetic,
standards-compliant SARIF and explicit current/producer contexts.

## Goals / Non-Goals

**Goals:**

- Add readable Core reference scenarios that execute the established reader → selector → normalized
  projection → applicability flow.
- Cover trusted findings, trusted zero results, required evidence failures, stable hashes,
  deterministic selection/deduplication, baseline identity, and equivalent human/JSON/SARIF/Testing
  facts.
- Keep the external-evidence guide vendor-neutral and explicit that SARIF is evidence, not an
  analyzer integration.

**Non-Goals:**

- Altering policy schema, public API, trust decisions, fingerprint semantics, or native finding
  behavior.
- Running, installing, authenticating to, or fetching artifacts from third-party analyzers.
- Creating a competing external-diagnostic result, identity, baseline, or applicability model.

## Decisions

### 1. Add a composition-focused test fixture instead of expanding unit-test matrices

The new test fixture will construct small SARIF 2.1.0 documents with the existing test repository,
then pass the resulting trusted reads through the production selector and projections. This keeps
the evidence path visible in one place and avoids duplicating the reader/selector's exhaustive
edge-case unit matrices.

### 2. Assert canonical typed projections, with sink parity as a shared invariant

Reference assertions will compare logical evidence ID, producer/run, repository/revision/scope,
artifact hash, source and fallback fingerprints, canonical finding identity, and applicability
state. Human, JSON, SARIF, and Testing checks will prove the same imported finding facts are
available without embedding an opaque source SARIF document.

### 3. Exercise failures before normalization

Missing, malformed, failed, incomplete, wrong-key, wrong-repository, wrong-revision, wrong-scope,
and missing-required-binding scenarios will assert an unassessable applicability record and no
selected or normalized imported finding. This reflects the established ownership boundary: the
reader decides trust; later components preserve its outcome.

### 4. Preserve documentation as the public reference for the protocol

The policy-format guide will explicitly label the flow as a synthetic, vendor-neutral reference
scenario. It will name only SARIF, explicit contexts, and canonical ArchLinterNet outputs; it will
not introduce a branded analyzer example or operational instructions that imply analyzer control.

## Risks / Trade-offs

- [Reference test becomes a second implementation] → Use existing production types and compact
  helpers, while leaving detailed parser/filter edge cases in their established suites.
- [Output assertions become brittle] → Assert structured normalized properties and key human
  markers, not serialized formatting incidental details.
- [Scenario coverage obscures the trust boundary] → Keep invalid evidence assertions at the reader
  and applicability seam and assert that selector/normalization receive no findings.
- [Large scenario fixture breaches file-size policy] → Keep helpers focused and split only if the
  test exceeds the repository's warning threshold.
