## Why

Coverage contracts (namespace and rule-input scopes) already produce findings, but those findings are only ever shown as a flat violation list. Teams adopting architecture coverage have no way to ask "how much of this repo is covered by policy?" — there is no summary of covered/excluded/uncovered/stale/unknown counts, no surfaced exclusion reasons, and no stable machine-readable shape for CI dashboards. Issue #102 asks for that summary view so coverage adoption and review don't require manually reconstructing state from individual findings.

## What Changes

- Add a coverage summary report that aggregates existing coverage engine output into deterministic counts per dimension: namespace and rule-input (the only implemented scopes today), plus explicit "not yet supported" placeholders for project, assembly, and dependency-edge scopes so the report's shape is stable as those scopes land later.
- Counts cover: covered, excluded (with reason text), uncovered, stale, and unknown — using existing `ArchitectureCoverageExclusion.Reason` data and existing findings, not new validation logic.
- Add deterministic human-readable CLI output for the coverage summary (stable ordering, readable for PR review).
- Add JSON output for the coverage summary suitable for CI/dashboards, including per-item uncovered evidence and exclusion reasons.
- Wire the summary into the existing `validate` CLI command's output modes (`--format human|json`) without changing existing violation/cycle output shapes.
- Update public docs (`docs/contracts/coverage.md`, `docs/usage/output-formats.md`, `docs/cli/index.md`, `mkdocs.yml` nav) to describe the new summary output.

## Capabilities

### New Capabilities
- `architecture-coverage-reporting`: deterministic coverage summary computation (counts, exclusions+reasons, uncovered evidence) and its human/JSON CLI rendering.

### Modified Capabilities
(none — this only adds a new reporting capability on top of existing coverage contract behavior; no existing requirement changes)

## Impact

- `src/ArchLinterNet.Core/Reporting/` — new coverage summary builder + formatter additions.
- `src/ArchLinterNet.Core/Execution/` — read-only consumption of `ArchitectureCoverageInventory` and existing coverage findings; no changes to coverage validation logic itself.
- `src/ArchLinterNet.Cli/Program.cs` — wire summary output into `validate` command output.
- `tests/ArchLinterNet.Core.Tests/`, `tests/ArchLinterNet.Cli.Tests/` — new tests for empty repo, fully covered, partially covered, audit-only, and JSON output cases.
- `docs/` — coverage, output-formats, CLI reference pages + mkdocs nav.
