## Context

The manual release workflow (`.github/workflows/release-nuget.yml`) currently restores, builds, and packs NuGet packages without running the repository acceptance gate. The CI workflow already runs `make acceptance` for pull requests and main branch pushes, but the release workflow skips it entirely.

The acceptance gate (`make acceptance` = `make lint` + `make test`) requires:
- .NET SDK 10.0.x (already in the workflow)
- `uv` (Python package manager, via `astral-sh/setup-uv` action — used by CI workflow but absent from release workflow)

The release process doc already specifies acceptance as a prerequisite before packaging, but the workflow doesn't enforce it.

## Goals / Non-Goals

**Goals:**
- Add `make acceptance` step to the release workflow before any `dotnet pack` step
- Add `setup-uv` step to the release workflow (required for `lint-code-size` and `lint-docs` inside `make acceptance`)
- Fail the release if acceptance fails (standard GitHub Actions step-failure behavior)
- Keep dry-run (`publish=false`) behavior: artifacts still produced, but only after acceptance passes

**Non-Goals:**
- Changing the CI workflow (`.github/workflows/ci.yml`)
- Changing acceptance criteria, lint rules, or test suite definition
- Reworking package metadata, versioning, NuGet login, or publish logic
- Refactoring GitHub Pages deployment
- Adding tag-triggered releases or release automation

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Gate position | After `Build`, before first `Pack` | Build already compiled with release version. `dotnet test --no-restore` won't rebuild stale binaries. Directly mirrors "validate before packaging" intent. |
| Command | `make acceptance` | Canonical repo acceptance gate (`lint test`). Single command consistent with CI workflow. |
| Step ordering | `Build` → `Acceptance` → `Pack *` | Pack steps use `--no-build` and depend on the Release build output. Acceptance validates the versioned build. |

## Risks / Trade-offs

| Risk | Impact | Mitigation |
|------|--------|------------|
| `setup-uv` adds ~15s to workflow setup | Minor delay | Already present in CI workflow. Negligible for a manual release run. |
| `make acceptance` adds ~1-2 min | Longer release run | Acceptable for a manual workflow. CI already runs the same gate. |
| `lint-docs` could fail on unrelated doc issues | Release blocked by doc formatting | This is correct behavior — broken docs should be fixed before release. |
| Workflow file doesn't exist locally | Cannot verify YAML syntax locally | Fetch from raw GitHub for editing; validate by triggering workflow run. |
