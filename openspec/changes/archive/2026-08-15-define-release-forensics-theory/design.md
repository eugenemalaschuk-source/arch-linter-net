## Context

Issue #235 establishes the theory contract for Release Architecture Forensics
before implementation begins. The current product evaluates present-state
architecture contracts; the planned feature evaluates evidence accumulated in
an explicit Git range. The downstream implementation tasks need one source of
truth for deterministic input identity, zero/absent-evidence semantics,
ranking, and cautious report language.

The feature does not yet exist. Public MkDocs pages must describe implemented
product behavior, while this issue needs durable contributor-facing guidance.

## Goals / Non-Goals

**Goals:**

- Define deterministic, Git-only canonical evidence and score semantics.
- Fix the initial score profiles, normalization functions, ordering, and
  evidence vocabulary that #236 through #243 will consume.
- Preserve a clean boundary between the Git/history core, optional .NET
  enrichment, schema-backed configuration, and presentation.
- Provide a discoverable internal reference and a validated OpenSpec capability
  specification.

**Non-Goals:**

- Implement Git ingestion, CLI parsing, policy schema, scoring, reports, or
  Roslyn enrichment.
- Create a second configuration language, a separate executable, or a public
  documentation promise for unimplemented behavior.
- Treat heuristic output as a formal proof of coupling, merge conflicts, or an
  Open/Closed Principle violation.

## Decisions

### Keep the theory in a new capability specification and an internal reference

The new `release-architecture-forensics` spec captures testable requirements;
`docs/internal/release-forensics.md` is the readable, formula-oriented
implementation reference. This follows the repository's public/internal
documentation boundary and lets the guide be linked from `docs/internal/README.md`.

Alternative considered: a public `docs/guides/` page. Rejected because the
feature is planned, not shipped, and the public site must not present roadmap
behavior as current product capability.

### Resolve all canonical inputs before analysis

The canonical identity records explicit authored refs and resolved commit IDs,
the effective history-analysis configuration identity, and tool version.
Logical repository-relative paths, normalized author identities, sorted task
references, and deterministic commits are canonical evidence. Absolute checkout
paths, generated-at timestamps, local locale, and process environment are not.

Alternative considered: treating the current checkout time and path as report
metadata. Rejected because they would make identical analysis results differ
without a source or configuration change.

### Use total functions and fixed effective weights

Every normalizer returns `0` for an empty/all-zero population. Missing optional
evidence is raw `0`; it does not alter configured weights. The initial profiles
are fixed here and later become schema-configurable through #237, which owns
validation and bounded configuration syntax.

Alternative considered: renormalizing available components. Rejected because a
score would then mean different things in ranges with and without task evidence.

### Use transparent, bounded proxies instead of inferred design truth

Hotspots measure repeated edits and change volume. Co-change measures repeated
joint edits. Bottlenecks combine cross-task, author, temporal, and graph
evidence. OCP pressure adds a small, reported role/name-hint component. Findings
and recommendations preserve components and caveats rather than claiming a
formal architectural violation.

Alternative considered: LLM-generated classifications or unconstrained name
heuristics. Rejected because canonical results must be repeatable and reviewable.

### Make optional .NET enrichment strictly downstream

Git evidence must remain useful if a C# file cannot be parsed. Project,
namespace, and type data enrich a finding only after file-level scores are
complete; it cannot remove, reorder, or manufacture the core evidence.

## Risks / Trade-offs

- [Task references may be absent or inconsistently authored] → Treat their
  metrics as deterministic zero, retain the fixed weights, and report the
  limitation.
- [High churn may be mechanical work] → Preserve path categories and separate
  or suppress non-production findings through the later policy model.
- [Name hints can overfit repository vocabulary] → Bound their contribution to
  10%, report matched tokens, and state that they are heuristic evidence.
- [Co-change components can form overly broad clusters] → Require an explicit
  configured significance threshold before emitting clusters; always retain
  pair-level evidence.
- [Time zones and clock precision can vary] → Compare authored Git timestamps
  as UTC Unix seconds and use only the explicit analyzed range.

## Migration Plan

1. Archive this documentation contract into the main OpenSpec capability.
2. #236 implements deterministic Git-range ingestion against the canonical
   entities and range semantics.
3. #237 supplies the validated `history_analysis` configuration and bounded
   path classifier, including any thresholds/overrides.
4. #238 through #243 implement scoring, reporting, and optional enrichment
   without changing the theory contract silently; a future change is required
   for any semantic change.

No deployment or rollback action is required because this change adds no
runtime behavior.

## Open Questions

- None for the first deterministic profile. Later changes may revise defaults
  only through a reviewed schema/spec update with migration notes.
