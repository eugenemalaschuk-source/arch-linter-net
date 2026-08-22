## Context

`HistoryIngestionService` already creates the finalized, fail-closed canonical
Git evidence used by co-change, bottleneck, and OCP analyses. The existing
writers expose that data as an implementation-era ingestion view and a short
text summary. They do not retain hotspot analysis in the result, version a
successful report, model an enrichment state, or create candidates.

The report is a Core projection. The CLI remains a thin command boundary that
selects JSON or Markdown and preserves the existing output/error-stream split.
No renderer may read Git, policies, a worktree, or Roslyn.

## Goals / Non-Goals

**Goals:**

- Produce one versioned, explicit-schema JSON report from finalized evidence.
- Render a deterministic Markdown explanation from the same report input.
- Make candidate qualification, evidence IDs, caveats, configuration identity,
  and optional-enrichment status auditable.
- Keep report construction structurally downstream of every fail-closed
  condition.

**Non-Goals:**

- Recompute Git, metadata, TaskKey, path, rename, churn, graph, or score data.
- Add .NET/Roslyn enrichment or make it required.
- Add report files, configurable presentation options, revision-expression
  support, candidate LLM inference, or automatic refactoring.

## Decisions

### Report projection owns the versioned schema

`HistoryForensicsReportJsonWriter` replaces the ingestion JSON writer as the
successful CLI artifact. Its schema starts with `schemaVersion`, `kind`,
`historySemanticsVersion`, and `toolVersion`; all subsequent property order is
fixed in writer code. It emits `analysis` (range, object format, exact counts,
and canonical effective configuration), then ordered upstream evidence,
findings, enrichment, and candidates.

This is preferred to serializing anonymous models or maps: the existing
`CanonicalJsonWriter` already guarantees the required byte profile and explicit
write order makes schema review straightforward.

### Enrich the finalized result, never the renderer

`HistoryIngestionResult` gains the effective validated configuration, hotspot
analysis, and a typed `HistoryEnrichmentProjection`. The pipeline creates
`not_requested` today. The projection reserves status, bounded reason, ordered
provenance, and a future deterministic context payload. #242 can populate that
input later without inventing a second report schema.

This is a narrow new model because the current issue explicitly requires a
versioned enrichment boundary; passing untyped dictionaries into the renderer
would make ordering and future compatibility ambiguous.

### Candidates are report projections with stable source references

Every report finding gets a stable ID based on its finding kind, category/cohort,
and canonical path(s). Candidate records reference those IDs rather than copy
or rescore evidence. Positive hotspot, bottleneck, and OCP scores qualify using
the explicit exclusive-zero threshold. Every existing `Gtheta` cluster qualifies
because it already satisfied its canonical configured threshold. Candidates
record their exact components/threshold/cohort evidence plus fixed caveat IDs.

This produces useful investigations while ensuring empty qualifying sets stay
empty. No candidate changes a score, rank, group, or upstream ordering.

### Markdown is a read-only view of report input

Markdown is written by a separate Core renderer from the same finalized result;
it has sections for identity, hotspots, co-change, bottlenecks, OCP pressure,
candidates, enrichment, and interpretation limits. It never supplies inputs to
the JSON writer. There are no rendering options in this change, so its use
cannot alter canonical JSON bytes.

### The CLI publishes `history analyze`

The command’s behavior is now report-oriented, so `history analyze` replaces
the internal `history ingest` subcommand. It accepts the same explicit range,
repository, and policy inputs with `json` (default) or `markdown` formats.
Failure still writes only the existing deterministic JSON diagnostic to stderr;
successful report writers are not invoked.

## Risks / Trade-offs

- [Large explicit schema] → Keep the existing canonical writer and factor only
  repeated small output helpers; add byte and ordering goldens.
- [Schema drift as new enrichment arrives] → Include schema/version/enrichment
  envelope now and add tests that the default Git-only report is unchanged by
  future status-only states except for that projection.
- [Candidates interpreted as conclusions] → Use fixed caveat IDs in JSON and
  mandatory human interpretation notes in Markdown/docs.
- [Command rename affects pre-release callers] → Update CLI docs and focused
  command tests in the same change; no compatibility alias is introduced before
  the report contract is established.
