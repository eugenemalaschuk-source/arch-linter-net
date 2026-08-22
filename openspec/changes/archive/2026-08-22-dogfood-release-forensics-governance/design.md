## Context

The completed #235–#243 increments already implement the v1 semantics and
contain focused unit vectors for ref resolution, metadata, path/rename/churn,
scoring, reporting, and enrichment. Issue #244 is the release-integration
slice: it must preserve those semantics while proving the shipped command on a
real ArchLinterNet range and making the architecture of the delivered module
enforceable.

The current `v0.3.1` and `v0.4.0` tags are available locally. The release range
is historical, so requested .NET enrichment is expected to be unavailable when
the checked-out worktree does not equal the resolved `to` commit; that is an
intentional Git-only success case, not an analysis failure.

## Goals / Non-Goals

**Goals:**

- Capture deterministic, repository-safe release-range evidence from the
  shipped `history analyze` command using separate canonical operands.
- Close the remaining cross-cutting conformance gaps with focused tests that
  prove environment-independent canonical JSON and non-interference from both
  available and unavailable enrichment.
- Make History's canonical scalar ordering a neutral primitive so reporting
  does not import the raw Git ingestion namespace, then enforce the completed
  History and CLI module directions with existing strict dependency contracts.

**Non-Goals:**

- Change scoring, thresholds, configuration schema, canonical Git semantics,
  public API, or report schema.
- Add Git revision-expression compatibility, path-lifetime segmentation, or
  rename-DAG timestamp repair.
- Introduce a new architecture-policy contract family or test-only rule.

## Decisions

### Retain a concise dogfood summary, not a generated report artifact

The committed artifact will record the canonical inputs, SHA-256 identities of
the generated JSON, selected canonical findings, manual comparison, limitations,
and enrichment outcomes. It will not commit the entire generated report, which
would duplicate a deterministic runtime artifact and add review noise. The
summary includes exact commands so it can be reproduced from the public range.

### Prove enrichment non-interference by comparing the report outside its projection

Focused NUnit coverage will render one finalized Git result with the
`not_requested`, `available`, and `unavailable` projections. It will compare
the report portions before the `enrichment` property byte-for-byte and assert
the expected distinct enrichment projections. This directly protects the
#242/#243 hand-off without reconstructing or rescoring output in the test.

### Extract scalar ordering from raw Git decoding

`GitPathDecoder` currently owns both strict path-byte decoding and the general
Unicode-scalar comparator used by Git, scoring, task ordering, and reporting.
Move the comparator to `History.Canonical` and update call sites. This is the
smallest concrete separation needed for reporting to be independent of raw Git
ingestion while keeping its mandated scalar ordering exact.

### Use strict dependency rules for module governance

Add explicit namespace layers and `strict` dependency rules to the existing
self-policy. They prohibit raw Git ingestion from importing analysis/reporting/
enrichment, scoring/evidence from importing reporting or enrichment, optional
enrichment from importing report rendering, report rendering from importing raw
Git ingestion, and the CLI History command from importing internal History
implementation modules. The parent `History` namespace remains the composition
surface that coordinates the delivered pipeline.

## Risks / Trade-offs

- [Historical enrichment cannot materialize from the current checkout] → record
  its deterministic `revision_mismatch` status and prove the available path with
  a fixture whose resolved `to` equals its clean checkout.
- [A report comparison accidentally compares enrichment itself] → use the
  writer's fixed property order and isolate only the prefix preceding the
  `enrichment` property; separately assert the projection differences.
- [A new policy rule is too broad for current intentional dependencies] → base
  each forbidden target on inspected imports and include it in rule-input
  coverage, so it fails closed if the layer is renamed or becomes empty.

## Migration Plan

1. Add the neutral canonical comparator and update its internal callers.
2. Add the two focused release-closure test vectors and strict policy layers.
3. Build and run the real CLI range with no enrichment and requested enrichment;
   capture the deterministic summary and verify the expected Git-only result.
4. Synchronize and archive the OpenSpec change after validation.

Rollback is a normal revert: no persisted data, schema, or public API migration
is involved.
