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
1. OpenSpec lifecycle.
1. Implementation lifecycle.
1. Validation lifecycle.
1. Pull request lifecycle.

Do not reorder or skip phases. Keep the user informed at meaningful phase boundaries and stop only for a real access, safety, source-of-truth, or environment blocker.

## 1. Branch lifecycle

Complete this phase before changing files.

1. Determine the repository root, current branch and HEAD, remotes, and working-tree status.
1. Preserve all existing user changes.
1. Never implement directly on `main`.
1. If on `main`, fetch fresh `origin/main`, create an issue-specific feature branch from it, verify checkout, and only then edit files.
1. If already on another branch, continue only when it belongs to the supplied issue. If it is unrelated and cannot be changed safely, report the blocker and stop.
1. Never open a feature PR from `main`.

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
1. `opsx-propose`
1. `opsx-apply`
1. implementation
1. tests
1. spec synchronization
1. `opsx-archive`
1. PR

Specs must exist before implementation. Synchronize them after implementation and tests. Archive only after synchronization and before opening the PR. Do not move archive work after PR creation or merge.

When OpenSpec does not apply, explicitly record `OpenSpec: not applicable` and explain why.

## 3. Implementation lifecycle

Before coding:

1. Summarize the issue objective and acceptance criteria.
1. Identify affected components and architecture boundaries.
1. Inspect neighboring implementations and tests.
1. State scope and non-goals.
1. Provide a short implementation plan.

During coding:

- implement the complete current issue scope;
- follow existing project patterns and boundaries;
- keep changes local;
- prefer existing abstractions, explicit code, and typed APIs;
- update relevant tests and documentation;
- avoid unrelated cleanup, speculative abstractions, opportunistic refactoring, and unrequested architecture changes.

Before adding an abstraction, layer, service, interface, manager, facade, or extension point, explain which concrete requirement in the current issue it solves. Do not add it without such a requirement.

## 4. Validation lifecycle

Implementation is incomplete until validation succeeds. Local validation is risk-based: scope it to the change instead of always running the entire repository suite. Exhaustive, cross-platform validation is authoritative in PR CI, not a local ritual — see [Risk-based local validation](#risk-based-local-validation) below.

Always required locally, for every change:

1. Add or update the tests that prove the issue behavior.
1. Run the most focused relevant test/filter first.
1. Run `make fmt` (or the equivalent formatter) on changed files and inspect the formatting diff.
1. Run relevant OpenSpec validation when OpenSpec applies.
1. Run any repository lint directly implicated by the change.

Then expand validation according to the change's risk tier, per the table below. Fix issue-related failures and rerun validation until the locally-required checks pass.

Do not open a PR when the locally-required checks for the change's risk tier were not executed, related failures remain, or success cannot be established. For an environment blocker, report the exact command, observed failure, unvalidated scope, and required prerequisite.

### Risk-based local validation

Classify the change into exactly one tier and apply its required local validation. When a change spans tiers, use the higher tier.

**Focused / localized change** — one contract handler or validator, one CLI command path, one CEL parser/evaluator behavior, a documentation-only change, or a workflow-only change:

- focused changed/new tests;
- the directly affected test project/family, where reasonably bounded;
- relevant formatter/linter/spec checks.
- Full `make acceptance` is **not mandatory** before opening a PR.

**Cross-cutting change** — shared Core infrastructure, public API or schema changes, cache/build-state/cancellation/concurrency primitives, broad policy-loading/import behavior, or changes spanning multiple production projects:

- focused tests;
- all directly affected project/family suites;
- relevant lint/spec checks;
- broader validation when impact cannot be bounded confidently.
- `make acceptance` is recommended when it materially increases confidence, but is not a ritual prerequisite when CI is available and exhaustive CI will run before merge.

**Release / publication / CI-authority change** — release-candidate or packed-artifact authorization, publishing/versioning, changes to CI topology or required checks themselves, or cases where CI cannot exercise the relevant environment:

- the broadest locally available validation appropriate to the task;
- keep any existing release-specific full gates mandatory.

### Opening the PR without a full local `make acceptance`

A PR may be opened after successful risk-appropriate local validation even when `make acceptance` was not run. When doing so:

- list the exact local commands run and their results in the PR body;
- state explicitly when exhaustive validation is delegated to CI, e.g.: `Local validation: focused Core tests + make fmt + OpenSpec validation` / `Full acceptance: delegated to PR CI`;
- never fabricate or imply a full local pass that did not happen;
- do not wait for CI completion before opening the PR, unless the issue specifically requires CI evidence before the PR can be meaningfully created.

`make acceptance` remains the full local repository gate. It stays available and unchanged, and is expected for maintainer/manual verification, cross-cutting or release work where it materially increases confidence, and any case where CI is unavailable.

## 5. Spec synchronization and archive

When OpenSpec applies:

1. Compare implementation against the proposal and specs.
1. Update specs to describe actual behavior.
1. Remove unimplemented claims and add tested guarantees or edge cases.
1. Keep terminology consistent across code, tests, specs, and docs.
1. Run OpenSpec validation.
1. Execute and verify `opsx-archive`.
1. Inspect the synchronized and archived files.

Do not create the PR before this phase completes.

## 6. Pull request lifecycle

Before opening the PR, verify:

- the issue scope is complete;
- the branch is issue-specific and is not `main`;
- tests were updated where needed;
- `make fmt` passed;
- the [risk-based local validation](#risk-based-local-validation) required for the change's tier passed — `make acceptance` is only required when the tier mandates it;
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

List exact validation commands and results, and state which validation is delegated to CI (see [Opening the PR without a full local `make acceptance`](#opening-the-pr-without-a-full-local-make-acceptance)). Do not hide unfinished issue scope as a follow-up.

## Decision bias

Prefer simple over complex, local over global, existing patterns over new patterns, explicit code over magic, typed APIs over string flags, existing abstractions over new abstractions, and current requirements over hypothetical future needs.

## Completion report

After opening the PR, report the issue, branch, final commit, PR URL, implementation summary, OpenSpec result, tests changed, exact validation results, and remaining risks. Never report the workflow complete unless the PR was actually opened.
