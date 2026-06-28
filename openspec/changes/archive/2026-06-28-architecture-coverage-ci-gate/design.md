## Context

`ArchLinterNet.Cli` already supports `--policy`, `--mode strict|audit`, `--format human|json`, `--baseline`, and emits `coverage_summary`/`coverage_findings` in its JSON output (`ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts`). The repository's own CI (`ci.yml`) runs `make acceptance` (lint + test) but never invokes the CLI against `architecture/dependencies.arch.yml` directly, never uploads JSON artifacts, and has no PR-facing reporting. The repository's own policy currently defines no `strict_coverage`/`audit_coverage` contracts, so the coverage gate will pass trivially on this repo today — the gate must still work for repos (or future contracts) that do define them.

The repo build tool is `make` (no Task runner). Tooling scripts that aren't C# already live under `tools/scripts/` and run via `uv`/`tools/pyproject.toml` (see `lint_csharp_file_size.py`).

## Goals / Non-Goals

**Goals:**
- Run strict + audit architecture validation against the self-policy in CI and publish both as JSON artifacts on every PR and `main` push.
- Fail the PR (closed) when strict mode reports violations or new non-baselined coverage findings; never let audit mode block.
- Generate a concise Markdown report (matching the issue's example) and keep a single PR comment up to date across pushes.
- Report new-code coverage for changed first-party files, explicitly marking unmappable files as `unknown`.
- Add a README badge for the new gate, distinct from build/test status.

**Non-Goals:**
- Adding coverage contracts to `architecture/dependencies.arch.yml` (the self-policy). Out of scope — this change is CI plumbing only.
- A hosted dashboard, external SaaS integration, or generated percentage badge service.
- Dependency-edge coverage (already covered by #100/#101, unaffected here).
- Touching `.github/workflows/ci.yml` or its acceptance gate.

## Decisions

- **Separate workflow file (`architecture-coverage.yml`) instead of a job inside `ci.yml`.** GitHub/shields workflow badges reflect a whole workflow's latest run status, not an individual job's. A dedicated workflow gives a badge that means exactly "architecture coverage gate," and keeps `ci.yml` untouched per the precedent set by `ci-release-gate` (existing CI workflow should not be modified by unrelated CI changes).
- **Python script for report generation, not a new C# CLI subcommand.** The report generator is pure JSON-in/Markdown-out plus a `git diff`-based file-to-unit mapping — a CI-only concern with no reuse inside `ArchLinterNet.Core`/`Cli`. The repo already has a parallel precedent (`lint_csharp_file_size.py`) for Python CI helpers under `tools/scripts/`, run via `uv`, avoiding a new dotnet-tool-restore step just for report formatting.
- **`actions/github-script` for the sticky PR comment, not a third-party marketplace action.** Avoids adding a new external Action dependency for a single, simple "find comment by marker, update or create" operation.
- **New-code coverage mapping is best-effort and conservative.** A changed `.cs` file is mapped to a namespace via the first `namespace` declaration found in the file (regex), and to a project/assembly by walking up to the nearest `.csproj`. If neither match is found, or if the matched unit isn't present in `coverage_summary`, the file is reported as `unknown` — never assumed covered. This matches the issue's explicit instruction ("If mapping ... is not reliable yet, report it as unknown rather than pretending it is covered").
- **Strict JSON failure still uploads its own artifact.** The strict step uses `continue-on-error: true` purely so the artifact-upload and PR-comment steps still run on a failing gate; the job's overall conclusion is forced to `failure` via a final step that checks the strict step's outcome, so the workflow (and badge) still reports failure.

## Risks / Trade-offs

- [Risk] New-code coverage mapping false negatives (real coverage exists but file→namespace mapping is wrong) → Mitigation: always prefer reporting `unknown` over a wrong `covered`/`uncovered` claim; document the limitation in `ci-integration.md`.
- [Risk] Sticky PR comment step requires `pull-requests: write` permission, slightly widening the workflow's permission scope beyond today's read-only CI → Mitigation: scope the permission narrowly to the new workflow only, not `ci.yml`.
- [Risk] Two workflows both build the CLI on every PR (duplicated build time) → Mitigation: acceptable trade-off for badge isolation; build is incremental/cached by `actions/setup-dotnet`.

## Migration Plan

No data migration. Rollout is additive: new workflow file, new script, README/docs edits. Rollback is deleting the new workflow file and reverting docs/README changes.

## Open Questions

None — scope confirmed against the issue and existing repo conventions during exploration.
