# Contributing to ArchLinterNet

ArchLinterNet is an early-preview, solo-maintainer project. Contributions are
welcome, but the operating model is small: there is no dedicated review team
and no fixed response-time commitment. See [SECURITY.md](SECURITY.md) for how
that applies to vulnerability reports specifically.

## Before you open a pull request

1. Open or find a tracking issue first for anything beyond a trivial fix, so
   scope and approach can be agreed before code is written.
1. Read [AGENTS.md](AGENTS.md) — it documents the project layout, key `make`
   commands, and coding conventions used throughout the codebase.
1. Follow [Pull Request Governance](docs/ai/pull-request-governance.md) for
   how a pull request should reference the issue it implements.

## Requirements for acceptable contributions

- **Coding conventions.** Private fields use `_camelCase`; types and members
  use `PascalCase`; interfaces are prefixed `IName`. See the
  [Conventions section of AGENTS.md](AGENTS.md#conventions).
- **Formatting.** Run `make fmt` before submitting; CI enforces
  `dotnet format` cleanliness.
- **Warnings as errors.** `TreatWarningsAsErrors` is enabled solution-wide in
  `Directory.Build.props`. A change that introduces a compiler warning will
  fail the build.
- **Tests.** Add or update tests for behavior changes. C# tests use
  **NUnit**; run the focused test project with
  `dotnet test tests/<Project> --no-restore` before running the full suite.
  Python release/tooling scripts under `tools/` use **pytest**; run them
  with `make test-release-evidence`, `make test-calculate-version`,
  `make test-coverage-badge-script`, or `make test-tooling-coverage` for
  the full Python suite with coverage.
- **Architecture governance.** Changes must satisfy the repository's own
  architecture policy: run `make lint-architecture` (read-only) before
  submitting. See `architecture/dependencies.arch.yml` for the enforced
  rules.
- **File size.** Keep files under the repository's size thresholds: C#
  files warn at 500+ lines and fail (CI-blocking) only above 800 lines —
  exactly 800 still passes; Markdown files warn at 600+ lines and fail
  only above 1000 — exactly 1000 still passes. `make lint-code-size`
  checks both.
- **Public API changes.** If a change affects a reviewed public API surface,
  see the reviewed public API lifecycle in [AGENTS.md](AGENTS.md) — snapshot
  rewrites (`make public-api-update`) are a separate, explicit step and are
  never implied by `lint` or `acceptance`.

Run `make lint` (or the more targeted commands above) locally before opening
a pull request; `make acceptance` runs the full local gate but is not
required for every change — see the
[feature implementation workflow](docs/ai/feature-implementation-workflow.md)
for risk-based local validation guidance.

## Reporting bugs and requesting enhancements

Use the GitHub issue tracker for both. For a suspected security
vulnerability, use private reporting as described in
[SECURITY.md](SECURITY.md) instead of a public issue.

## Backlog conventions

If you are proposing new tracked work rather than a direct pull request, see
[Backlog governance](docs/ai/backlog-governance.md) for the issue title and
structure conventions this repository uses.
