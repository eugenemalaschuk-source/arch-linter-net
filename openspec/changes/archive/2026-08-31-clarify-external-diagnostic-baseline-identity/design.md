## Context

The selected imported-diagnostic identity already distinguishes producer tool name/version and
the resolved repository, revision, and scope context. The baseline projector reuses that identity.
Run ID, repository-relative artifact path, and artifact SHA-256 remain provenance facts so that
an equivalent producer rerun does not become artificial debt. The live specification and external
evidence guide describe this boundary too broadly.

## Goals / Non-Goals

**Goals:**

- State the exact baseline identity dimensions in the live behavior contract and public guide.
- Keep the distinction testable through the existing V2 tool-version/rerun regression coverage.
- Preserve immutable history by recording the correction as a new OpenSpec change and archive.

**Non-Goals:**

- Change selector, mapper, baseline-projector, or schema behavior.
- Redefine producer identity or migrate existing baseline entries.
- Rewrite a prior archived change.

## Decisions

### 1. Document the two categories explicitly

The correction will enumerate the transient fields excluded from baseline identity (run ID,
artifact path, and artifact content hash) rather than referring broadly to producer/run or artifact
provenance. It will separately name tool name/version and repository/revision/scope as intentional
identity dimensions. This aligns user expectations with the persisted identity instead of asking
them to infer it from implementation details.

### 2. Amend the existing federation requirement

The external-diagnostics federation capability already owns end-to-end baseline projection.
Modifying that requirement, with an observable rerun-versus-producer/context scenario, keeps the
contract adjacent to the feature it corrects. A new capability would duplicate the same lifecycle.

### 3. Reuse the established regression test

The existing selector/projector/baseline test compares a tool-version change against changes to
run ID and artifact provenance. It is the direct executable evidence for this documentation-only
correction; no production-path change or duplicate test fixture is needed.

## Risks / Trade-offs

- [Readers interpret all provenance as transient] → enumerate the identity-bearing producer and
  context dimensions next to the excluded transient fields.
- [Future implementation drifts from the documentation] → validate the exact existing regression
  test and retain its scenario in the live OpenSpec requirement.
- [Historical change is rewritten] → archive this new delta only; leave prior archive content
  untouched.
