## Context

The release-forensics pipeline currently exposes canonical file evidence and
heuristic scoring under `History.Analysis`. The existing policy only prevents
that combined module from depending on reports or enrichment, so it cannot
prevent a scorer from coupling to construction internals or changing canonical
file semantics without distinct review.

## Goals / Non-Goals

**Goals:**

- Make canonical file evidence a separate namespace and policy layer.
- Preserve one-way scoring-to-evidence consumption while prohibiting evidence
  construction from depending on scorers.
- Keep scoring from bypassing final evidence to raw Git ingestion.
- Record strict OpenSpec validation for the correction.

**Non-Goals:**

- Change canonical file identity, rename, churn, scoring, report, or CLI
  semantics.
- Make public API or schema changes.

## Decisions

- Move commit/file/rename/churn/co-change construction types into
  `ArchLinterNet.Core.History.Evidence`; retain findings and scorers in
  `ArchLinterNet.Core.History.Analysis`. This aligns namespace ownership with
  the finalized-data boundary and keeps consumers explicit.
- Name the self-policy layers `core_history_evidence` and
  `core_history_scoring`. An evidence-to-scoring forbidden edge prevents a
  silent feedback path, while scoring-to-evidence remains allowed.
- Prohibit scoring from importing raw Git, reports, and enrichment. This forces
  scorers to consume finalized data from Evidence rather than reconstructing
  inputs.

The alternative of retaining one `Analysis` layer with documentation-only
guidance would not make a reverse dependency fail the strict architecture gate.

## Risks / Trade-offs

- [Internal namespace move could miss a consumer] → Compile and run focused
  History tests plus the strict architecture gate.
- [Policy may overconstrain legitimate composition] → Keep root `History` as
  the explicit composition seam and allow scoring-to-evidence only.
- [Dogfood semantics could be changed under pressure] → Add a normative
  separate-specification/migration requirement.

## Migration Plan

The namespace types are internal, so no consumer migration is required. Move
the types and imports atomically, run focused validation, then archive the
OpenSpec change. Reverting the commit restores the former namespace layout and
policy rules.
