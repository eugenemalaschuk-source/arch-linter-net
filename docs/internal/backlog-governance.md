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

## Milestone and release lifecycle rules

The canonical lifecycle and milestone semantics are defined in [Release lifecycle and milestone governance](release-lifecycle-governance.md). Apply that document when planning, assigning, or interpreting release-related backlog.

Key rules:

- a milestone is a **development-wave envelope and traceability aid**, not public-release authority;
- milestone membership alone never makes an issue release-blocking or part of an immutable candidate;
- a release milestone may intentionally contain release-required capability work, explicitly non-blocking hygiene, and post-release stabilization work at the same time;
- the `X.Y.0` minor release may therefore be published while architecture/quality cleanup attributable to that wave remains open in the milestone;
- real-adoption correctness blockers are functional stabilization work, not optional technical-debt cleanup;
- post-release architecture cleanup should precede the authoritative whole-repository Sonar/maintainability sweep;
- patch releases form a maintenance train as needed; there is no requirement for exactly one patch after a minor;
- next-minor backlog planning may overlap stabilization, but unrelated next-minor product bytes must not make a truthful current-line patch impossible unless an explicit maintenance-line strategy exists.

When an issue participates in a release lifecycle, state its role explicitly in the body where useful: `release-blocking product capability`, `consumer-shaped acceptance`, `optional/non-blocking release hygiene`, `real-adoption correctness stabilization`, `post-release architecture cleanup`, `final Sonar/quality cleanup`, or `maintenance publication`.

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

For post-release architecture cleanup, record the exact analyzed release range and distinguish debt created/materially amplified by that release from older debt merely touched by it. Do not expand a release-specific cleanup into a general rewrite without evidence and explicit ownership.

## Release task rules

Release tasks must preserve both the [release lifecycle](release-lifecycle-governance.md) and pipeline separation:

- PR CI validates code and documentation only.
- PR CI must not build or publish official release packages.
- Manual release workflow owns official package build, NuGet publication, GitHub Release creation, and docs publication.
- Dry-run release paths must not publish packages or create public releases.
- Public publication must use one calculated version consistently for packages, tags, artifacts, release notes, and docs.
- Milestone membership is discovery/traceability metadata, never release-scope authority.
- Correctness, security, and release-integrity defects must not be mislabeled as optional cleanup to preserve a release date.
- Broad behavior-preserving refactoring may remain outside the minor critical path only when the debt is visible, bounded, and assigned to an explicit stabilization path.
- Real-adoption defects discovered after `X.Y.0` belong to focused patch stabilization until the promised workflow is adoption-stable.
- Whole-repository Sonar cleanup runs after structure-changing architecture stabilization by default.
- A patch candidate must not silently contain unrelated next-minor feature bytes; follow the maintenance-line rules and patch guard instead of narrowing the release declaration cosmetically.

## Documentation boundary rule

Backlog governance is internal project documentation. It must not appear in the public MkDocs navigation or be linked from NuGet.org as product documentation.

Public AI docs may describe policy authoring and policy review. Internal agent workflow docs stay in internal Markdown files.
