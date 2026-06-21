# Backlog Governance and Issue Authoring

This is internal project documentation for maintaining the `arch-linter-net` repository. It is intentionally excluded from the public MkDocs/GitHub Pages product site.

## Roadmap hierarchy

```text
Roadmap / strategic theme
  -> Story / umbrella issue
      -> Task / child issue
          -> PR
```

Use a story when the work groups multiple child tasks. Use a task when the work is focused enough to implement and review in one PR.

## Issue title convention

All new backlog issues should include a hierarchy marker and a work type marker.

```text
[STORY][AI|HYBRID|MANUAL] <Area>: Clear story title
[TASK][AI|HYBRID|MANUAL] <Area>: Clear task title
```

Recommended areas:

- `Tooling` — engine, CLI, schema, release, CI, docs, packaging, validation;
- `Docs` — documentation-only work;
- `Release` — operational release work when `Tooling` would be misleading.

Prefer precise verbs such as `Design`, `Document`, `Define`, `Implement`, `Add`, `Integrate`, `Create`, `Audit`, `Validate`, or `Polish`.

Avoid vague titles such as `Improve release workflow`, `Performance foundation`, or `Support external packages` when a narrower action is possible.

## Work type markers

### `[AI]`

Mostly suitable for AI/Codex/opencode implementation: documentation, tests, C# engine/CLI code, YAML schema changes, package metadata, and validation wiring.

### `[HYBRID]`

AI-friendly work with a meaningful manual validation or setup component, such as publication flows requiring NuGet.org or GitHub Release inspection.

### `[MANUAL]`

Mostly manual operational work, such as secret provisioning, trusted publishing setup, or repository settings changes.

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

Stories should include a child-task checklist and recommended sequence when order matters.

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

Omit empty dependency or related lines when not needed.

## Estimate rule

Estimate only the developer's real hands-on time assuming AI assistance is available.

```markdown
## Estimate

Developer time with AI assistance: X-Yh.
```

Do not estimate AI compute time, waiting time, or theoretical team effort.

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

Release tasks must preserve pipeline separation:

- PR CI validates code and documentation only.
- PR CI must not build or publish official release packages.
- Manual release workflow owns official package build, NuGet publication, GitHub Release creation, and docs publication.
- Dry-run release paths must not publish packages or create public releases.
- Public publication must use one calculated version consistently for packages, tags, artifacts, release notes, and docs.

## Documentation boundary rule

Backlog governance is internal project documentation. It must not appear in the public MkDocs navigation or be linked from NuGet.org as product documentation.

Public AI docs may describe policy authoring and policy review. Internal agent workflow docs stay in internal Markdown files.
