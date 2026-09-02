## Why

The repository currently posts a standalone Architecture Coverage comment directly from the
untrusted pull-request workflow. The Core/CLI renderer now produces the complete canonical
architecture pull-request report, but CI has no safe path to publish that artifact without giving
the PR-code execution context write permission.

## What Changes

- Generate the canonical Health, change, and CLI-rendered architecture PR report in a read-only
  CI producer job and publish it with a fixed, manifest-bound artifact shape.
- Add one separate minimal-permission `workflow_run` publisher that verifies the current PR/head,
  producer run/attempt, artifact identity, bounded shape, manifest schema, and SHA-256 before
  creating or updating the single sticky report comment.
- Safely surface bounded transport/integration failure in place of stale evidence; never render
  substitute architecture semantics in workflow code.
- Migrate the existing repository-owned Architecture Coverage sticky-comment path to the unified
  report while retaining standalone coverage reporting and raw strict/audit/coverage artifacts.
- Document the producer/publisher trust boundary, including safe fork behavior.

## Capabilities

### New Capabilities

- `architecture-pr-report-publication`: secure, manifest-bound GitHub publication of the
  Core/CLI-rendered architecture PR report.

### Modified Capabilities

- `github-actions-ci`: move PR comment write authority out of the read-only CI workflow and add
  the report-artifact producer topology.
- `architecture-coverage-ci-reporting`: retire the competing Architecture Coverage sticky comment
  while preserving coverage artifacts and the standalone coverage command.

## Impact

- `.github/workflows/ci.yml` and a new publisher workflow.
- Workflow contract tests and CI integration documentation.
- No public C# API, report-rendering semantics, or external-service integration changes.
