## Context

`architecture/policy/audit-conventions.arch.yml` already declares
`production-types-have-one-source-declaration` with `max_declarations_per_type: 1` in
`audit_layout_conventions`, and the engine (`LayoutConventionDeclarationCountChecker`) already
computes an exact declaration count and file list per type and embeds them in each violation's
`forbidden_reference` text and canonical finding identity (`declaration-count:{expected}:{actual}`).
Running `--mode audit --contract production-types-have-one-source-declaration --format json`
against current `main` returns exactly 19 offending production types (see proposal), each with a
stable, deterministic message. `decompose-god-classes` (still active, unarchived) owns replacing
these aggregates with named collaborators; #742 asks for a ratchet that stops the count from
growing while that refactor continues, without completing it here.

## Goals / Non-Goals

**Goals:**

- Fail `make lint-architecture` the moment a handwritten production partial aggregate is
  introduced, or an existing one gains another declaration.
- Accept today's 19 known aggregates without requiring `decompose-god-classes` to finish first.
- Reuse only already-shipped engine behavior (`ignored_violations` exact matching); add no new
  Core capability, schema field, baseline file, or metric kind.
- Keep `decompose-god-classes` the sole authority for eventually removing this debt entirely.

**Non-Goals:**

- Completing `decompose-god-classes` extractions (tasks 2.4/2.5/3.x/4.x remain open there).
- Zeroing all 19 offenders before v0.8 closes.
- A general LOC/complexity/god-object budget.
- A new baseline file, metric kind, or per-type numeric-override schema field.

## Decisions

### 1. A second strict rule, not converting the existing audit rule

`production-types-have-one-source-declaration` stays in `audit_layout_conventions` exactly as
today, so it keeps reporting the complete debt inventory `decompose-god-classes` targets (`max: 1`,
no exceptions). A new, separately-authored `strict_layout_conventions` entry — same selector, same
`max_declarations_per_type: 1` — is the ratchet. Converting the existing rule in place would
conflate "the eventual target" with "does not regress today," and would require this change to also
edit `decompose-god-classes`' own task list to stay coherent. Two rules evaluating the same evidence
from different modes is an existing pattern in this file (folder-purity rules are strict while
naming/model-placement rules stay audit).

### 2. Freeze via exact-match `ignored_violations`, not a new engine capability

Three mechanisms were considered for "accept today's count, reject growth":

- **A version-3 metric baseline** (`architecture-metric-baseline-gates`) — rejected: the metric
  catalog (`architecture-metric-semantics`) is closed and has no "declarations per type" kind;
  adding one is a materially larger, separately-scoped change.
- **A per-type numeric override field on `ignored_violations`** (e.g. `max_declarations: <int>`,
  matched by count rather than text) — considered, and would tolerate partial reductions more
  gracefully, but requires touching the shared `ArchitectureIgnoredViolation` model, the shared
  ignore-matching code path, and the shared JSON schema `ignoredViolation` def used by every
  contract family. That is a new engine capability, not the "smallest self-policy regression" the
  issue asks for when the generic mechanism already suffices.
- **Plain `ignored_violations` entries with today's exact `source_type` and exact
  `forbidden_reference` text** (chosen) — the checker's `forbidden_reference` already encodes the
  exact count and the full sorted file list (`"expected at most 1 source declaration(s), found N:
  <paths>"`), and this text becomes part of the violation's canonical identity
  (`declaration-count:{expected}:{actual}`). An entry pinned to today's exact text only matches
  while the type's declaration set is unchanged. Any added, removed, or renamed declaration changes
  the text, so the frozen entry stops matching and the (new) violation surfaces — this is true for
  a strictly worse change and equally true for a strictly better one, so it does not distinguish
  growth from improvement by itself.

### 3. Improvement is accepted through the existing unmatched-ignore signal, not silently

`unmatched_ignored_violations` already defaults to `error` repository-wide (no self-policy override
exists or is added here). Once a type's declarations no longer match its frozen entry, the run
fails either because the (still-violating, differently-worded) finding is unignored, or — once the
type is fully fixed to one declaration and the checker stops emitting a candidate for it at all —
because the now-permanently-unmatched `ignored_violations` entry itself fails the gate. Either
outcome is a **visible, actionable** one-line diff (update or delete the frozen entry) in the same
PR that made the improvement, consistent with how this repository already treats other reviewed,
exact artifacts (e.g. `public-api-surface` snapshots with `api_comparison: exact`). This matches the
decompose-god-classes remediation shape (`extract`/`replace`, not incremental file-by-file
shrinking), so the realistic path to "reduced" is "the type stops being partial," where zero
extra engineering is needed for either direction.

## Risks / Trade-offs

- [A cosmetic rename of one of the 19 frozen files breaks the gate even though the count didn't
  change] → accepted: consistent with this repository's existing exact-reviewed-artifact
  philosophy (public API snapshots behave the same way), and it is a one-line policy fix, not a
  code change.
- [A future contributor copies the numeric-override idea into another family's `ignored_violations`
  usage] → not applicable here: no new field is introduced, so there is nothing to copy.
- [19 near-identical `ignored_violations` entries look repetitive] → accepted: each is independently
  reviewable, disappears individually as `decompose-god-classes` lands, and needs no shared
  abstraction for 19 static, rarely-touched lines.

## Migration Plan

None: additive self-policy-only change. No runtime, schema, or public API impact.
