## Context

Issue #235 establishes the theory contract for Release Architecture Forensics
before implementation begins. The current product evaluates present-state
architecture contracts; the planned feature evaluates evidence accumulated in
an explicit Git range. The downstream implementation tasks need one source of
truth for deterministic input identity, rename/category semantics,
normalization populations, task-independence evidence, effective scoring,
ranking, and cautious report language.

The feature does not yet exist. Public MkDocs pages must describe implemented
product behavior, while this issue needs durable contributor-facing guidance.

## Goals / Non-Goals

**Goals:**

- Define deterministic, Git-only canonical evidence and score semantics.
- Fix initial default score profiles while allowing #237 to provide one
  validated effective profile per run.
- Define normalization populations so path noise cannot accidentally change a
  different category's scores.
- Define independent-task evidence so multi-reference commits do not manufacture
  parallel-development pressure.
- Preserve a clean boundary between Git/history core, optional .NET enrichment,
  schema-backed configuration, and presentation.
- Provide a discoverable internal reference and validated OpenSpec capability.

**Non-Goals:**

- Implement Git ingestion, CLI parsing, policy schema, scoring, reports, or
  Roslyn enrichment.
- Create a second configuration language, separate executable, or public
  documentation promise for unimplemented behavior.
- Treat heuristic output as formal proof of coupling, merge conflicts, or an
  Open/Closed Principle violation.

## Decisions

### Keep theory in a capability specification and internal reference

The `release-architecture-forensics` spec captures testable requirements;
`docs/internal/release-forensics.md` is the readable formula-oriented reference.
This follows the repository's public/internal documentation boundary and keeps
planned behavior outside public MkDocs navigation.

Alternative considered: a public `docs/guides/` page. Rejected because the
feature is planned, not shipped.

### Resolve all canonical inputs before analysis

Canonical identity records explicit authored refs and resolved commit IDs,
effective history-analysis configuration identity, and tool version. Logical
repository-relative paths, normalized authors, ordered task references, and
deterministic commits are canonical evidence. Absolute checkout paths,
generated timestamps, locale, timezone, and process environment are not.

Alternative considered: checkout time/path as canonical metadata. Rejected
because equivalent analysis would differ without source/configuration change.

### Give each rename chain one canonical to-side path

One unambiguous linear rename chain is one logical file. Its canonical path is
the path at the last in-range occurrence, including a deleted path when deletion
is last. Earlier paths remain aliases. The primary path category is derived from
the canonical path. Ambiguous copy/split/merge relationships remain separate
identities instead of guessing a representative.

Alternative considered: classify each alias independently and aggregate later.
Rejected because #236 and #237 could then choose different ranking paths and
primary categories for identical evidence.

### Define score populations after analysis filtering and by category cohort

#237 ignore rules are analysis filters and remove files before graph and score
population construction. Presentation suppression is downstream and cannot
change scores. File metrics normalize against retained files in the same primary
category; co-change edge metrics normalize against retained edges with the same
unordered endpoint-category pair.

Alternative considered: one repository-wide maximum. Rejected because generated,
docs, tests, or build churn could depress production normalized components even
when those findings are reported separately.

### Use total functions and effective weights

Every normalizer returns `0` for empty/all-zero population. Missing optional
evidence is raw `0`; it does not alter effective weights. The specification owns
initial defaults, while #237 may expose validated configuration. Formulas always
consume effective weights and never hard-code defaults as unconditional runtime
constants.

Alternative considered: renormalizing remaining components when evidence is
missing. Rejected because a score would then mean different things in ranges with
and without task evidence.

### Separate ordinary task references from independent-work evidence

A commit may reference multiple issues and legitimately contribute ordinary task
spread/co-change evidence to each. It cannot by itself establish independent
workstreams. Two task refs become an independent pair for a file only if each has
pair-exclusive commits touching that file. Temporal proximity and repeated-edit
signals use pair-exclusive evidence.

Alternative considered: treat every pair of extracted refs as separate task
episodes for bottleneck scoring. Rejected because one multi-reference commit
would create maximum temporal overlap and false parallel-development pressure.

### Use transparent, bounded role-token proxies

Role/name hints tokenize the canonical file stem deterministically at
non-alphanumeric, camel/Pascal, acronym-to-word, and letter/digit boundaries,
then use invariant-lowercase exact token equality. Substring, glob, and regex
matching are excluded. Matched tokens are reported and the default contribution
remains bounded to 10%.

Alternative considered: unspecified substring/name heuristics. Rejected because
`OrderService`, `DiagnosticMapper`, and similar names would otherwise produce
implementation-dependent scores.

### Make optional .NET enrichment strictly downstream

Git evidence remains useful if a C# file cannot be parsed. Project, namespace,
and type data enrich a finding only after file-level scores are complete; they
cannot remove, reorder, or manufacture core evidence.

## Risks / Trade-offs

- [Task references may be absent/inconsistent] → Treat missing metrics as zero,
  retain effective weights, and report the limitation.
- [A commit may mention several task refs] → Preserve ordinary breadth evidence
  but require pair-exclusive evidence for parallel-development/OCP signals.
- [High churn may be mechanical work] → Use path categories and category-cohort
  normalization; #237 may explicitly ignore noise sources.
- [A rename crosses categories] → Canonical to-side classification is stable and
  aliases remain visible, but historical category context may still require
  human interpretation.
- [Name hints can overfit vocabulary] → Exact bounded token matching, reported
  matches, and 10% default weight cap.
- [Co-change components can form broad clusters] → Require an explicit effective
  significance threshold; always retain pair evidence.
- [Time zones/clock precision vary] → Compare Git timestamps as UTC Unix seconds
  and use only the explicit analyzed range.

## Migration Plan

1. Archive this documentation contract into the main OpenSpec capability.
2. #236 implements deterministic Git-range ingestion, canonical rename identity,
   and task-reference evidence.
3. #237 supplies validated `history_analysis` configuration, ignores,
   classification, thresholds, and effective scoring profiles.
4. #238/#239 implement hotspot and co-change evidence against the defined
   category-cohort populations.
5. #240/#241 consume independent-task evidence for bottleneck/OCP pressure.
6. #242/#243 add optional enrichment and stable reports without silently changing
   the theory contract; semantic changes require reviewed spec updates.

No deployment or rollback action is required because this change adds no runtime
behavior.

## Open Questions

- None for the first deterministic profile. Later changes may revise defaults or
  evidence semantics only through a reviewed schema/spec update with migration
  notes.
