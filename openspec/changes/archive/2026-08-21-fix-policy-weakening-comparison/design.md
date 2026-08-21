## Context

The initial #119 implementation correctly separates static policy semantics
from fact-dependent selector membership, but its exception and project-glob
projections were too lossy for that boundary. The fixes span public context
models, comparison logic, test approvals, and self-policy fixtures.

## Goals / Non-Goals

**Goals:**

- Make universal ignored-violation detection use typed effective evidence.
- Detect explicit required-to-optional relaxations without evaluating code.
- Preserve the bounded trust boundary for project-discovery glob changes.
- Restore approval and exhaustive-namespace CI coverage.

**Non-Goals:**

- Implement glob containment or candidate-policy simulation.
- Infer selector/project membership from validation output or display strings.
- Change ordinary strict/audit validation or baseline lifecycle.

## Decisions

- Emit context v3 with a typed `IgnoredViolation` projection containing the
  source and forbidden-reference matchers. This separates executable matchers
  from the readable `Details` field; parsing display strings was rejected as
  non-canonical and brittle.
- Compare required-to-optional only for typed source sets, layer-template
  entries, and coverage optional-input declarations. These are explicit
  directionally ordered policy facts. Generic fact diffs remain unsupported
  unless their family establishes a direction.
- Emit changed project include/exclude globs as `impact_not_proven`. A context
  contains patterns but not the resolved project universe, so literal set
  comparison and glob-containment guesses were rejected.

## Risks / Trade-offs

- [Context v2 artifacts are rejected] → v3 is explicit and fail-closed; users
  regenerate both artifacts with the same supported CLI.
- [Bounded glob findings can require review] → this is preferable to a false
  semantic failure or silently missing scope reduction.
- [Public Core API grows] → update both reviewed API snapshots deliberately.

## Migration Plan

1. Regenerate base and current context artifacts with policy context v3.
2. Run the weakening comparison and review any bounded glob findings.
3. Roll back by restoring the previous policy/CLI version; no policy migration
   or baseline rewrite is performed.
