## Context

`HistoryIngestionJsonWriter` currently owns the canonical report envelope and
the JSON details for every analysis family. Candidate and enrichment projection
already demonstrate the intended focused collaborator boundary, but the
remaining configuration, evidence, scoring, and graph families still converge
in one large writer. The report is a versioned consumer artifact: any change to
the section order, property order, canonical scalar formatting, stable IDs, or
terminal LF is a compatibility regression.

## Goals / Non-Goals

**Goals:**

- Preserve the exact v1 canonical JSON bytes while reducing reporting-side
  change coupling.
- Keep `HistoryIngestionJsonWriter` as the sole composition boundary for the
  envelope, version fields, deterministic section order, and terminal text.
- Give each natural report family one focused internal projection collaborator.
- Keep identity/category/task-key/value helpers in one reporting-only seam.
- Prove the complete representative projection and the small composition shape
  with focused tests.

**Non-Goals:**

- Change report schema, version, scoring, finding/ranking/candidate semantics,
  evidence construction, Git ingestion, policy loading, or public APIs.
- Introduce a generic serialization framework, runtime plugin mechanism, or
  reporting dependency on Git or enrichment implementation code.

## Decisions

1. The top-level writer will write only the fixed envelope and invoke the
   existing/enclosed section collaborators in the current canonical order. This
   preserves one visible ordering authority without keeping all section detail
   in a shared implementation hub. Moving envelope fields into a collaborator
   was rejected because it would obscure the versioned report boundary.

2. Reporting will have focused internal writers for analysis/configuration,
   canonical evidence (commits, rename records, logical files), hotspots,
   co-change, bottlenecks, and OCP pressure. Candidate and enrichment writers
   remain their current independent projections. These boundaries match report
   sections and finalized result families rather than introducing a generic
   serializer abstraction.

3. A small shared reporting helper will own the existing canonical category
   strings, finding/cluster identities, task-key output, stable string-array
   output, and line-status text. This preserves the established calculations
   and prevents independently evolving sections from copying them.

4. Focused regressions will create one deterministic fixture with configuration,
   commits, exact rename provenance, logical files, hotspot/co-change/
   bottleneck/OCP findings, enrichment, and candidates. Its canonical bytes
   will be retained as a hash golden while focused assertions ensure each
   section is populated. A structural test will constrain the top-level writer
   to its small composition method set and verify the complete delegate order.
   A monolithic literal JSON fixture was rejected because it would make a
   reporting-only refactor needlessly difficult to diagnose while adding no
   stronger byte guarantee than a deterministic byte digest.

## Risks / Trade-offs

- [Moving code reorders a property or changes a helper call] → retain the
  existing serialization statements intact within their owning projection and
  prove the representative report's exact bytes.
- [Shared helper becomes a new broad abstraction] → limit it to existing
  stateless canonical value/identity operations and keep section traversal in
  focused writers.
- [Future section logic drifts back into the composition writer] → enforce its
  declared method shape and ordered collaborator calls in a structural test.
- [A collaborator reads live inputs or recalculates findings] → accept only a
  finalized `HistoryIngestionResult` or finalized section analysis data; retain
  the strict reporting dependency policy gate.

## Migration Plan

1. Extract the shared reporting helpers and each natural projection family
   without changing their write order or statements.
2. Reduce the top-level writer to envelope composition and final text framing.
3. Add representative byte-golden and structural coverage, then run focused,
   architecture, code-size, public-API, and OpenSpec validation.
4. Archive the change to synchronize the implementation boundary requirement
   into the release-forensics reporting specification. Reverting the feature
   commit restores the previous implementation only; no data or consumer
   migration is required.
