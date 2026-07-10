## Context

The project has 348 open SonarCloud code smells (all CODE_SMELL, zero bugs/vulnerabilities/hotspots) with ~14h estimated remediation effort. PR quality gate consistently flags new-code issues, adding review friction. An active branch `task/249-sonarcloud-251-maintainability` has already resolved ~12 rounds of findings (S3267, S6607, S2325, S1192, S1481, S1172, cognitive complexity, duplication) — this change picks up everything remaining.

Key observation: **186 of 348 issues (53%) are CA1861** — `static readonly` array fields. These are mechanical, low-risk changes that eliminate the bulk of the debt quickly.

## Goals / Non-Goals

**Goals:**
- Eliminate all 348 open SonarCloud code smells
- Pass new-code quality gate with zero issues on future PRs
- Prioritize by impact: fix high-count/low-risk rules first (CA1861, CA1822), then medium-risk, then debatable items

**Non-Goals:**
- No behavioral or API changes
- No new capabilities or features
- No spec-level requirement changes
- No changes to SonarCloud quality gate configuration

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Order of work | By difficulty: mechanical → review-needed → debatable | Clear blockers out first; manual-review items benefit from remaining context |
| CA1861 approach | `static readonly` fields declared locally near usage (not a central `Constants` class) | Keeps change local, avoids cross-file coupling, easier to review |
| S107 (too many params) | Introduce parameter objects only where >10 params; for 8-10 params, suppress with `// NOSONAR` | Parameter objects for borderline cases add more complexity than they remove |
| S3011 (reflection) | Suppress with `// NOSONAR` where reflection is intentional (test fixtures, internal access patterns) | These are deliberate design choices, not accidents |
| S8677 (PowerShell) | Fix only; no style enforcement added to CI | Outlier rule; once fixed, treat as one-time cleanup |
| SYSLIB1045 (GeneratedRegex) | Apply `[GeneratedRegex]` where regex is used in hot paths or as static fields; leave instance regex as-is | Avoids premature optimization on rarely-called paths |
| Batch/PR strategy | One PR per rule family (CA1861, CA1822, manual-review) | Keeps diffs focused, easy to review and revert if needed |

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| CA1861 touches 186 sites across many files — risk of merge conflicts with in-flight work | Do CA1861 batch first and merge fast; coordinate with any active branches |
| S107 parameter objects could bloat the type surface if overused | Strict threshold: only >10 params. Suppress borderline cases. |
| S3011 suppressions hide future misuse | Add inline comment explaining why reflection is intentional |
| `TreatWarningsAsErrors` is enabled — some Roslyn CA fixes may interact with analyzer config | Run `make acceptance` after each batch before pushing |
| 26 PowerShell S8677 issues in CI scripts — fixing may break workflow | Test CI workflow changes by running `act` locally or pushing to a branch |
