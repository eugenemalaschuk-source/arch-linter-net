# Backlog Governance and Issue Authoring

ArchLinterNet uses an AI-first backlog structure so ChatGPT, Codex, opencode and future agents can create, split and update GitHub issues consistently.

This document is adapted from the First Ice backlog governance rules, but it is scoped to the public `arch-linter-net` tooling repository.

## Roadmap hierarchy

```text
Roadmap / strategic theme
  -> Story / umbrella issue
      -> Task / child issue
          -> PR
```

Use a story when the work groups multiple child tasks. Use a task when the work is focused enough to be implemented and reviewed in one PR.

## Issue title convention

All new backlog GitHub issues must include a hierarchy marker and a work type marker.

### Canonical title format

```text
[STORY][AI|HYBRID|MANUAL] <Area>: Clear story title
[TASK][AI|HYBRID|MANUAL] <Area>: Clear task title
```

`<Area>` should identify the production area. For this repository, use:

- `Tooling` — default for engine, CLI, schema, release, CI, docs, packaging, and validation work;
- `Docs` — documentation-only work that is not tied to implementation;
- `Release` — only when the issue is operational release work and `Tooling` would be misleading.

Prefer `Tooling` for most ArchLinterNet backlog entries so the backlog stays consistent with First Ice tool-adoption stories.

Examples:

```text
[STORY][AI] Tooling: Add advanced dependency governance contracts
[TASK][AI] Tooling: Add exhaustive container coverage checks
[TASK][AI] Tooling: Define generated release-note categories
[TASK][AI] Tooling: Add automatic version bump selection
[TASK][AI] Tooling: Document release publication workflow
```

Do not create untyped issue titles such as `Release notes`, `Performance`, `Support external packages`, or `Improve release workflow`.

## Issue title verb taxonomy

Use a small controlled verb set so backlog entries stay consistent and scannable.

### Preferred leading verbs

- `Design` — architecture, ADR, research, or technical decision work.
- `Document` — documentation-only work.
- `Define` — contracts, interfaces, enums, schemas, boundaries, categories, or rules.
- `Implement` — concrete runtime or domain behavior.
- `Add` — infrastructure, tooling, test harnesses, packages, workflow wiring, or validation features.
- `Integrate` — cross-boundary integration between existing systems.
- `Create` — generated artifacts, pages, dashboards, sites, or visual outputs.
- `Audit` — review existing backlog, code, or documentation for gaps and inconsistencies.
- `Validate` — prove an assumption, workflow, fixture, package, or integration path.
- `Polish` — readability, UX, or feedback pass after the core behavior exists.

### Avoid in new issue titles

Avoid vague verbs and nouns that do not describe the actual work slice:

```text
Introduce
Consume
Support
Foundation
Setup
Prepare
Improve
Enhance
```

Prefer precise replacements:

```text
Support external dependency checks
-> Add external dependency contracts

Improve release workflow UX
-> Add automatic version bump selection

Release notes categories
-> Define generated release-note categories

Performance foundation
-> Design lint performance and parallel execution plan
```

`Foundation` may still appear in the body when describing rationale, but avoid it as the main title noun.

## Work type markers

### [AI]

Mostly suitable for AI/Codex/opencode implementation.

Typical examples:

- architecture-contract model changes;
- documentation;
- tests;
- pure C# engine or CLI code;
- YAML schema changes;
- release workflow changes that can be dry-run validated;
- package metadata and docs wiring.

### [HYBRID]

Combination of AI-friendly code/design work and manual validation.

Typical examples:

- publication flows requiring manual NuGet.org or GitHub Release verification;
- release UX changes that require manual workflow UI checks;
- local environment validation requiring human inspection.

Use `[AI]` if the manual part is only normal dry-run validation already described in the issue.

### [MANUAL]

Mostly manual operational or environment work.

Typical examples:

- secret provisioning;
- NuGet.org trusted publishing/account configuration;
- repository settings changes that cannot be validated by tests.

## Estimate rule

All new issues should estimate only the developer's real hands-on time, assuming AI assistance is available.

Do not estimate AI compute time, waiting time, or theoretical studio/team effort.

Use this section in every story and task:

```markdown
## Estimate

Developer time with AI assistance: X-Yh.
```

For `[HYBRID]` and `[MANUAL]` tasks, break down the estimate when useful:

```markdown
## Estimate

Developer time with AI assistance: 2-4h.

Breakdown:

- AI-assisted code/docs: 1-2h
- Manual setup/validation: 1-2h
```

Estimates should represent potential real calendar effort for the solo developer, not idealized AI output speed.

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
## Estimate
## Acceptance criteria
## Validation
## Non-goals
```

For stories, `What to do` should include a child-task checklist and recommended sequence when task order matters.

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
## Estimate
## Acceptance criteria
## Validation
## Non-goals
```

Omit empty `Depends on` or `Related` lines when there is no dependency or related issue.

## Linking rules

Issue bodies should explicitly reference their parent and dependencies:

```text
Parent story: #...
Depends on: #...
Related: #...
```

Do not rely only on labels.

## Architecture governance task rules

Architecture-governance tasks must preserve the repository's contract model:

- ArchLinterNet remains YAML-first and declarative.
- Strict and audit semantics must stay explicit.
- Strict violations must fail strict validation.
- Audit diagnostics must not accidentally become strict failures.
- Diagnostics must remain deterministic and useful for CI and AI agents.
- JSON schema, docs, examples, and AI-facing guidance must be updated when supported policy fields change.
- Existing policies must remain backward compatible unless a deliberate migration note is documented.

## Release task rules

Release-related tasks must preserve the pipeline separation:

- PR CI validates code only.
- PR CI must not build official versioned release packages.
- PR CI must not publish to NuGet.org.
- Manual release workflow owns official package build, artifact upload, optional publication, GitHub Release creation, and docs publication.
- Dry-run release paths must not publish packages or create public releases.
- Public publication must use one calculated version consistently for packages, tags, artifacts, release notes, and docs.

## Future agent rule

Agents must not create isolated implementation issues without placing them inside an existing story or creating/proposing the story first.

Before creating or updating a GitHub issue, agents must check this document and apply:

1. canonical title format;
1. controlled title verbs;
1. explicit parent/dependency links;
1. required issue sections;
1. estimate rule;
1. architecture/release task rules where relevant.
