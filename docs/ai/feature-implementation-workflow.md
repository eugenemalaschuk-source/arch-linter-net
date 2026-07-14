# Feature Implementation Workflow

Use this workflow to implement exactly one GitHub issue end to end.

## Input

The user supplies exactly one GitHub issue URL:

```text
https://github.com/<owner>/<repository>/issues/<number>
```

Treat it as `ISSUE_URL`. Read the issue through the available GitHub integration or authenticated CLI. Do not ask the user to paste accessible issue content, repeat this workflow, or confirm that work should begin. Pull request URLs are not valid inputs.

## Mandatory lifecycle

Execute these phases in order:

1. Branch lifecycle.
2. OpenSpec lifecycle.
3. Implementation lifecycle.
4. Validation lifecycle.
5. Pull request lifecycle.

Do not reorder or skip phases. Keep the user informed at meaningful phase boundaries and stop only for a real access, safety, source-of-truth, or environment blocker.

## Command policy

Every supported shell command must use the `rtk` prefix. Read the repository instructions first and inspect `rtk --help` when needed. Use a direct command only when RTK cannot perform the operation, and record the reason. Never report success without observing the command result.

## 1. Branch lifecycle

Complete this phase before changing files.

1. Determine the repository root, current branch and HEAD, remotes, and working-tree status.
2. Preserve all existing user changes.
3. Never implement directly on `main`.
4. If on `main`, fetch fresh `origin/main`, create an issue-specific feature branch from it, verify checkout, and only then edit files.
5. If already on another branch, continue only when it belongs to the supplied issue. If it is unrelated and cannot be changed safely, report the blocker and stop.
6. Never open a feature PR from `main`.

Default invariant: one issue equals one feature branch and one pull request.

## 2. OpenSpec lifecycle

Before coding, read:

- the issue and acceptance criteria;
- materially linked issues;
- relevant OpenSpec specs and active change files;
- ADRs and architecture documents;
- relevant documentation;
- neighboring implementation and tests;
- repository agent and contributor instructions.

If sources conflict, identify the exact conflict, prefer explicit acceptance criteria and active specs over architectural preference, and do not invent requirements.

OpenSpec normally applies when the issue changes user-visible behavior, architecture boundaries, policy semantics, configuration or schema behavior, public APIs, documented guarantees, or an existing capability.

When OpenSpec applies, use this exact order:

1. `opsx-explore`
2. `opsx-propose`
3. `opsx-apply`
4. implementation
5. tests
6. spec synchronization
7. `opsx-archive`
8. PR

Specs must exist before implementation. Synchronize them after implementation and tests. Archive only after synchronization and before opening the PR. Do not move archive work after PR creation or merge.

When OpenSpec does not apply, explicitly record `OpenSpec: not applicable` and explain why.

## 3. Implementation lifecycle

Before coding:

1. Summarize the issue objective and acceptance criteria.
2. Identify affected components and architecture boundaries.
3. Inspect neighboring implementations and tests.
4. State scope and non-goals.
5. Provide a short implementation plan.

During coding:

- implement the complete current issue scope;
- follow existing project patterns and boundaries;
- keep changes local;
- prefer existing abstractions, explicit code, and typed APIs;
- update relevant tests and documentation;
- avoid unrelated cleanup, speculative abstractions, opportunistic refactoring, and unrequested architecture changes.

Before adding an abstraction, layer, service, interface, manager, facade, or extension point, explain which concrete requirement in the current issue it solves. Do not add it without such a requirement.

## 4. Validation lifecycle

Implementation is incomplete until validation succeeds.

Execute in order:

1. Add or update relevant tests.
2. Run focused tests when useful.
3. Run `rtk make fmt`.
4. Inspect formatting changes.
5. Run `rtk make acceptance`.
6. Fix issue-related failures.
7. Rerun validation until it passes.

Do not open a PR when formatting or acceptance was not executed, related failures remain, or success cannot be established. For an environment blocker, report the exact command, observed failure, unvalidated scope, and required prerequisite.

## 5. Spec synchronization and archive

When OpenSpec applies:

1. Compare implementation against the proposal and specs.
2. Update specs to describe actual behavior.
3. Remove unimplemented claims and add tested guarantees or edge cases.
4. Keep terminology consistent across code, tests, specs, and docs.
5. Run OpenSpec validation.
6. Execute and verify `opsx-archive`.
7. Inspect the synchronized and archived files.

Do not create the PR before this phase completes.

## 6. Pull request lifecycle

Before opening the PR, verify:

- the issue scope is complete;
- the branch is issue-specific and is not `main`;
- tests were updated where needed;
- `rtk make fmt` passed;
- `rtk make acceptance` passed;
- specs match implementation;
- `opsx-archive` completed when applicable;
- the diff contains no unrelated files, secrets, temporary files, or local artifacts.

Follow repository commit conventions, push the feature branch without rewriting shared history, and do not merge it.

Open exactly one PR targeting `main` unless repository instructions specify otherwise. Include `Closes #<issue-number>` when appropriate and use these body sections:

- `Summary`
- `Architecture notes`
- `Scope / non-goals`
- `Tests run`
- `Risks / follow-ups`

List exact validation commands and results. Do not hide unfinished issue scope as a follow-up.

## Decision bias

Prefer simple over complex, local over global, existing patterns over new patterns, explicit code over magic, typed APIs over string flags, existing abstractions over new abstractions, and current requirements over hypothetical future needs.

## Completion report

After opening the PR, report the issue, branch, final commit, PR URL, implementation summary, OpenSpec result, tests changed, exact validation results, and remaining risks. Never report the workflow complete unless the PR was actually opened.
