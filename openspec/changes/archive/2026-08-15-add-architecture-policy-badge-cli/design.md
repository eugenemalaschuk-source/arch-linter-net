## Context

The repository currently invokes two Python modules after strict validation:
`architecture_coverage_report.py` renders the full Markdown report and compact
PR comment, while `architecture_coverage_failures.py` groups diagnostics. Those
outputs describe native CLI JSON and are product-facing CI artifacts, so they
belong in the CLI rather than developer-only tooling.

## Goals / Non-Goals

**Goals:**

- Add a `badge architecture-policy` CLI command that validates strict policy and
  emits Shields endpoint JSON.
- Add a `coverage report` CLI command that faithfully renders the existing full
  and compact coverage-report forms from strict JSON.
- Preserve changed-file classification and explicit diff-unavailable semantics.
- Move CI, Make targets, documentation, and tests to the native command paths.

**Non-Goals:**

- Change policy/coverage semantics, report wording, GitHub comment behavior, or
  the public workflow-status badge URL.
- Add a hosted endpoint, new package, or runtime Python dependency.

## Decisions

### Use hierarchical standard CLI commands

`arch-linter-net badge architecture-policy` receives the policy/build-state
options needed to execute strict validation and prints Shields endpoint JSON to
stdout. `arch-linter-net coverage report` receives strict JSON plus optional
changed-files/diff-status inputs and prints Markdown to stdout or an explicit
output path. The compact-comment form is selected by
`--max-failure-diagnostics`.

This mirrors existing `baseline`, `policy`, and `public-api` command grouping
while reserving room for future native badges/reports without treating format
rendering as a hidden script API.

### Preserve report input/output compatibility

The coverage command parses the current CLI JSON schema with `JsonDocument` and
recreates the existing deterministic output: summary totals, failed-rule
grouping, evidence escaping/compaction, configured-scope classification,
test/fixture exclusion, and diff-unavailable message. Existing CI consumers and
the sticky comment keep their Markdown shape.

### Retire the Python report implementation

Once NUnit coverage demonstrates equivalent representative behavior, remove
both Python modules and their tests. Python remains only for unrelated tooling;
the CI report path no longer depends on it.

## Risks / Trade-offs

- [Report-parity regression] → Port current tests and golden-focused assertions
  to NUnit before removing Python files.
- [Large CLI command code] → Keep parsing/classification/rendering in focused
  internal files under `Commands.Coverage`, sharing no unrelated validator code.
- [Badge failure loses diagnostic detail] → the badge command's endpoint JSON
  is concise by design; normal `validate` JSON remains the detailed artifact.

## Migration Plan

1. Implement and test both native command paths.
2. Switch Make and GitHub Actions calls to the CLI.
3. Remove the Python report modules/tests and refresh documentation.
4. Validate report parity locally and in PR CI; reverting restores the prior
   committed command/script wiring without data migration.

## Open Questions

None.
