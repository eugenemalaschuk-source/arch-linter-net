# Backlog Governance and Issue Authoring

This is internal project documentation for maintaining the `arch-linter-net` repository. It is intentionally excluded from the public MkDocs/GitHub Pages product site.

## Purpose

Use this document to keep the ArchLinterNet backlog consistent across stories, tasks, pull requests, and internal planning discussions.

The backlog must stay useful for three audiences:

- the maintainer planning work;
- AI/Codex agents implementing scoped tasks;
- reviewers checking that each PR maps to one issue and one acceptance target.

## Roadmap hierarchy

```text
Roadmap / strategic theme
  -> Story / umbrella issue
      -> Task / child issue
          -> PR
```

Use a story when the work groups multiple child tasks or establishes a sequence. Use a task when the work is focused enough to implement and review in one PR.

A PR should normally close exactly one task. A story is closed only after its child tasks are complete or deliberately moved to another story.

## Current backlog map

Keep this section aligned when issues are moved between umbrellas.

### Adoption story

- [ ] #45 — [STORY][AI] Tooling: Add architecture policy adoption enhancements
  - [x] #44 — [TASK][AI] Tooling: Add automatic baseline generation
  - [x] #46 — [TASK][AI] Tooling: Add namespace glob patterns
  - [ ] #47 — [TASK][AI] Tooling: Add method-body detection for external dependency contracts

Scope rule: #45 is for adoption blockers that are already close to the existing policy model. New contract-family work belongs to #55 after the architecture refactor.

### Architecture refactoring story

- [ ] #69 — [STORY][ARCH] Refactor validation pipeline to reduce feature coupling

Recommended child-task sequence for #69:

- [ ] Extract shared validation application service.
- [ ] Introduce runtime contract catalog descriptors.
- [ ] Replace manual contract-family loops with a handler registry.
- [ ] Move ignore tracking and baseline collection into execution context.
- [ ] Introduce analysis session and lazy indexes.
- [ ] Split diagnostics model behind existing human/JSON formatters.
- [ ] Add ArchLinterNet self-policy to prevent central-orchestration regression.

Scope rule: #69 is architectural refactoring only. It should preserve YAML compatibility and existing CLI/test behavior. It should not introduce new functional contract families except as migration scaffolding.

### First-class functionality story

- [ ] #55 — [STORY][AI] Tooling: Reach first-class architecture linting functionality
  - [ ] #56 — [TASK][AI] Tooling: Add solution and project discovery
  - [ ] #57 — [TASK][AI] Tooling: Add architecture coverage contracts
  - [ ] #66 — [TASK][AI] Tooling: Add architecture policy consistency checks
  - [ ] #51 — [TASK][AI] Tooling: Add assembly independence contracts
  - [ ] #58 — [TASK][AI] Tooling: Add full assembly dependency contract families
  - [ ] #59 — [TASK][AI] Tooling: Add NuGet package dependency contracts
  - [ ] #60 — [TASK][AI] Tooling: Add external dependency allow-only contracts
  - [ ] #61 — [TASK][AI] Tooling: Add project-aware Roslyn analysis
  - [ ] #62 — [TASK][AI] Tooling: Add dependency graph export and explain command
  - [ ] #63 — [TASK][AI] Tooling: Add baseline lifecycle commands
  - [ ] #64 — [TASK][AI] Tooling: Align validator and testing APIs with CLI capabilities
  - [ ] #65 — [TASK][AI] Tooling: Add SARIF diagnostics output

Recommended sequence:

1. Finish #45.
2. Complete #69 before adding new contract families.
3. Implement #56, #57, and #66 before assembly/package boundary expansion.
4. Implement #51 as the first post-refactor assembly-boundary task.
5. Continue with #58 and #59.
6. Add #60, #61, #62, #63, #64, and #65 in that order unless a task is deliberately split.

Scope rule: #55 is functional. It must not absorb release automation, marketing, public launch, docs-site polish, or standalone performance optimization.

### Performance story

- [ ] #19 — [STORY][AI] Tooling: Design lint performance and parallel execution plan

Scope rule: performance work starts after the core feature semantics and validation pipeline are stable. Performance tasks must not change policy semantics unless the change is explicitly documented and reviewed.

## Issue title convention

All backlog issues should include a hierarchy marker, a track marker, an area, and a clear action phrase.

```text
[STORY][TRACK] Area: Clear story title
[TASK][TRACK] Area: Clear task title
```

Use the same title after the issue number in parent checklists.

### Hierarchy markers

- `[STORY]` — umbrella issue with child tasks and sequencing.
- `[TASK]` — independently implementable issue, normally one PR.

### Track markers

Use one primary track marker per issue:

- `[AI]` — mostly suitable for AI/Codex implementation.
- `[ARCH]` — internal architecture refactoring or architecture-boundary work.
- `[DOCS]` — documentation-only work.
- `[RELEASE]` — release, package, publication, or distribution workflow.
- `[HYBRID]` — AI-friendly work with meaningful manual validation or setup.
- `[MANUAL]` — mostly manual operational work, such as secrets or repository settings.

Prefer `[AI]` for functional tooling tasks unless the work is primarily internal refactoring, documentation-only, release-only, or manual operations.

### Areas

Recommended areas:

- `Tooling` — engine, CLI, schema, validation, policy model, diagnostics, testing adapter.
- `Architecture` — internal code architecture, dependency direction, refactoring, self-policy.
- `Docs` — documentation-only work.
- `Release` — NuGet, GitHub Release, versioning, publication, distribution.
- `CI` — pull-request gates, workflow reliability, acceptance automation.

Prefer precise verbs such as `Add`, `Extract`, `Introduce`, `Replace`, `Move`, `Align`, `Document`, `Validate`, `Refactor`, or `Polish`.

Avoid vague titles such as `Improve workflow`, `Performance foundation`, or `Support packages` when a narrower action is possible.

## Parent, dependency, and relation lines

Use these metadata lines at the top of issue bodies:

```markdown
Parent story: #...
Depends on: #...
Related: #...
```

Rules:

- A task should have exactly one `Parent story` unless it is intentionally standalone.
- Use `Depends on` only when work should not start before another issue is complete.
- Use `Related` for context links that are useful but not blocking.
- When a task moves between stories, update both the task body and both parent story checklists.
- Do not leave a task checked under two active parent stories.

## Required issue structure

### Story template

```markdown
Related: #...
Depends on: #...

## Goal
## Work type
## Context
## What to do
## Manual tasks
## AI-friendly tasks
## Recommended sequence
## Acceptance criteria
## Validation
## Non-goals
```

Stories should include a child-task checklist and a recommended sequence when order matters.

Use `## Estimate` only when it materially helps planning. Prefer removing stale estimates over preserving inaccurate ones.

### Task template

```markdown
Parent story: #...
Depends on: #...
Related: #...

## Goal
## Work type
## Context
## What to do
## Manual tasks
## AI-friendly tasks
## Acceptance criteria
## Validation
## Non-goals
```

Omit empty dependency or related lines when not needed.

Use `## Estimate` only when the task has a meaningful planning estimate.

## Estimate rule

Estimate only the developer's real hands-on time assuming AI assistance is available.

```markdown
## Estimate

Developer time with AI assistance: X-Yh.
```

Do not estimate AI compute time, waiting time, or theoretical team effort. Update or remove estimates when a task is moved to a different story and the sequencing assumptions change.

## Architecture governance task rules

Architecture-governance tasks must preserve the repository's contract model:

- ArchLinterNet remains YAML-first and declarative.
- Strict and audit semantics must stay explicit.
- Strict violations must fail strict validation.
- Audit diagnostics must not accidentally become strict failures.
- Diagnostics must remain deterministic and useful for CI and AI agents.
- JSON schema, docs, examples, and AI-facing guidance must be updated when supported policy fields change.
- Existing policies must remain backward compatible unless a deliberate migration note is documented.
- New contract families should be added through the post-#69 catalog/handler seams, not by adding new manual execution loops to every entry point.

## Architecture refactoring task rules

Architecture refactoring tasks must reduce coupling without changing user-visible behavior unless a behavior change is explicitly accepted.

Required rules:

- Preserve existing CLI arguments, exit codes, and human/JSON output unless the task explicitly says otherwise.
- Preserve public validator and testing adapter compatibility.
- Prefer extraction and delegation before replacement.
- Keep each refactor PR reviewable and behavior-preserving.
- Add characterization tests when moving orchestration logic.
- Do not introduce IoC as a substitute for stable application, catalog, handler, and execution-context seams.

## Functional task rules

Functional tooling tasks must define policy shape, diagnostics, and validation evidence.

Required rules:

- Document the YAML shape before or during implementation.
- Update JSON schema for every supported policy-field change.
- Include green, violating, and edge-case tests.
- Include deterministic ordering in diagnostics and generated outputs.
- State non-goals clearly when a nearby capability is intentionally out of scope.

## Release task rules

Release tasks must preserve pipeline separation:

- PR CI validates code and documentation only.
- PR CI must not build or publish official release packages.
- Manual release workflow owns official package build, NuGet publication, GitHub Release creation, and docs publication.
- Dry-run release paths must not publish packages or create public releases.
- Public publication must use one calculated version consistently for packages, tags, artifacts, release notes, and docs.

## Documentation boundary rule

Backlog governance is internal project documentation. It must not appear in the public MkDocs navigation or be linked from NuGet.org as product documentation.

Public AI docs may describe policy authoring and policy review. Internal agent workflow docs stay in internal Markdown files.

## Backlog consistency checklist

Use this checklist when creating, moving, or closing issues:

- [ ] The title follows `[STORY|TASK][TRACK] Area: Action phrase`.
- [ ] The task has exactly one active parent story.
- [ ] Parent story checklists match the task body.
- [ ] Dependency lines are blocking dependencies, not loose context.
- [ ] Related lines are context-only.
- [ ] Acceptance criteria are testable.
- [ ] Validation names the acceptance gate or focused tests.
- [ ] Non-goals prevent scope creep.
- [ ] Moved issues are removed from the old parent checklist.
- [ ] Closed child tasks are checked in the parent story.
