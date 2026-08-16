## Context

`ci.yml` already validates the repository architecture policy in its pull-request
lint path and publishes architecture coverage artifacts, while its `main`
aggregation publishes test coverage to Codecov. Neither produces a stable,
product-specific public indicator of the repository's strict self-policy status.
The release workflow is the sole owner of GitHub Pages documentation deployment,
so using Pages as a frequently refreshed badge-data store would violate the
release-pipeline boundary.

## Goals / Non-Goals

**Goals:**

- Expose the latest default-branch strict self-policy outcome as an
  ArchLinterNet-specific README badge.
- Make the badge dynamic without writing generated values or refresh commits
  into the repository.
- Fail closed so a strict-policy failure is reflected as a failing workflow
  badge.
- Keep architecture policy, architecture coverage, and unit-test coverage as
  distinct signals.

**Non-Goals:**

- Add product contract families, schema fields, release-forensics behavior, or
  an architecture-coverage percentage badge.
- Publish GitHub Pages from ordinary CI or alter Codecov/SonarCloud behavior.
- Add a manually curated badge metric or a custom external service.

## Decisions

### Use a dedicated strict-policy GitHub Actions workflow as the badge source

Add `architecture-policy.yml`, triggered for pull requests and pushes to
`main`. Its single named job restores dependencies and runs the authoritative,
read-only `make lint-architecture` gate. The README uses GitHub's built-in
workflow badge URL pinned to `branch=main` and links to that workflow.

This is a first-class, deterministic CI result rather than a static artifact:
GitHub refreshes it for every default-branch run and renders failed strict
validation as a non-passing badge. A Shields endpoint or committed SVG/JSON
would need a new publication store and credential/write path; GitHub's workflow
badge has the precise pass/fail semantics required here with no repository
write.

### Keep the workflow narrow and independent

The workflow does not publish artifacts, packages, releases, Pages content, or
README changes. It is intentionally distinct from the pull-request
architecture-coverage report and Codecov test-coverage aggregation, so its
status cannot be mistaken for either metric.

### Test the deterministic interface through workflow and documentation checks

Add a small Python structural test that asserts the workflow triggers on
`main`, executes `make lint-architecture`, and the README's badge URL targets
that workflow and `main`. This protects the public contract without duplicating
the architecture validator.

## Risks / Trade-offs

- [The badge is status-only, not a coverage percentage] → The README and guide
  explicitly state that it proves strict self-policy passed and direct readers
  to the separate architecture-coverage report.
- [GitHub-hosted status depends on the latest default-branch workflow run] →
  The workflow runs on every push to `main` and no status can be hand-edited.
- [A new workflow can drift from its claimed gate] → The targeted structural
  test verifies the exact authoritative Make target.

## Migration Plan

1. Add the strict-policy workflow, test, Make target, and documentation.
2. Open the pull request; its workflow execution validates the new definition.
3. After merge, the first `main` run becomes the badge's source of truth.
4. Roll back by reverting the workflow and README change; no generated state or
   external store requires cleanup.

## Open Questions

None. GitHub's native workflow badge satisfies the issue's deterministic,
low-maintenance source requirement while preserving the repository's existing
release publication boundary.
