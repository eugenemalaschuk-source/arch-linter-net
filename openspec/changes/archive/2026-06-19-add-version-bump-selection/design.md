## Context

The manual release workflow (`release-nuget.yml`) currently accepts a free-text `version` input and passes it directly to the `PACKAGE_VERSION` environment variable used by all build and pack steps. This means the version calculation change is local — replace the input source without modifying downstream build/pack logic.

There are no existing git tags (no releases published yet). `Directory.Build.props` has `VersionPrefix=0.1.0`, but the release tooling source of truth should be git tags, not static project properties.

GitHub Actions already has `setup-uv` in the workflow, making Python a natural choice for the version calculation script.

## Goals / Non-Goals

**Goals:**

- Replace `version` free-text input with `release_type` choice dropdown (`preview`, `patch`, `minor`, `major`) plus optional `version_override` text input
- Create `tools/release/calculate_version.py` that detects latest SemVer git tag and calculates next version
- Create `tests/release/test_calculate_version.py` with unit tests for detection and increment rules
- Wire calculated version into `PACKAGE_VERSION` environment variable (existing downstream steps unchanged)
- Update `docs/reference/release-process.md` to document new dropdown-driven flow
- Validate version calculation early before build steps

**Non-Goals:**

- Modifying build, pack, or publish logic (already use `PACKAGE_VERSION`)
- Changing NuGet.org publication, trusted publishing, or docs deployment
- Tag automation or GitHub Release creation
- Supporting prerelease suffixes other than `preview.N`
- Making `make test` run Python tests (Python tests run as separate workflow step)

## Decisions

### Use Python script instead of inline bash or composite action

Python script at `tools/release/calculate_version.py` with `uv run`.

Alternatives considered:

- **Inline bash in workflow**: No testability, complex for SemVer sorting and numeric comparison.
- **.NET console tool project**: Adds compilation overhead (~10-15s) on every release workflow run; adds solution-level project that's unrelated to the main product.
- **Composite GitHub Action**: Premature abstraction — no reuse across workflows yet.
- **Bash script + bats tests**: Adds another test stack (bats) vs leveraging existing `uv` setup.

Python with `unittest` uses zero additional dependencies — `uv run python -m unittest discover -s tests/release` works with stdlib only.

### Python stdlib only, no external packages

Use `re`, `sys`, `unittest`, `subprocess`. No `packaging`, `semver`, or other PyPI packages.

The SemVer parsing needed is narrow — `X.Y.Z` with optional `-preview.N` suffix. Writing a `Version` dataclass with comparison is ~40 lines and gives precise control over numeric part comparison.

Alternatives considered:

- `pip install semver` or `packaging`: Adds a dependency for trivial parsing. Python stdlib handles this case perfectly.
- Custom parser: More code to maintain, but zero dependency risk and complete control.

### Tag format: `vX.Y.Z` and `vX.Y.Z-preview.N`

Tags use `v` prefix (e.g., `v0.1.0`, `v0.1.1-preview.1`). `PACKAGE_VERSION` output is always without `v` (e.g., `0.1.0`) — NuGet does not use `v` prefix.

The script accepts both `v` and non-`v` in `version_override` for flexibility, but strips `v` from output.

### No fallback to Directory.Build.props

If no valid SemVer tags exist and no `version_override` is provided, the script fails with a clear error. Git tags are the source of truth — static project properties can drift.

### Git tag fetching via `git tag` in subprocess

The script runs `git tag` and parses all tags, sorting them by SemVer precedence in Python. Not relying on `git tag --sort` (which sorts lexicographically, incorrectly for `v0.10.0` vs `v0.9.0`).

### Preview rules

- If latest tag is a preview (`vX.Y.Z-preview.N`), `preview` increments `N` by 1.
- If latest tag is stable (`vX.Y.Z`), `preview` produces `X.Y.(Z+1)-preview.1`.
- `minor` and `major` always produce stable versions, bumping from the base version (ignoring prerelease suffix).

### Patch after preview

`patch` finalizes the current preview train. If latest is `v0.1.1-preview.2`, patch produces `0.1.1`. This gives a clean chain:

```
0.1.0 → 0.1.1-preview.1 → 0.1.1-preview.2 → 0.1.1 → 0.1.2-preview.1 → ...
```

### No `patch`/`build` ambiguity

Dropdown uses `patch` only. No `build` alias to avoid confusion with SemVer build metadata.

### Workflow wiring — minimal diff

Only three changes to `release-nuget.yml`:

1. Input block: replace `version` with `release_type` + optional `version_override`
2. Checkout step: add `fetch-depth: 0` to get all tags
3. Replace version validation step with version calculation step that sets `PACKAGE_VERSION`

The `env.PACKAGE_VERSION: ${{ inputs.version }}` declaration at job level must be removed — `PACKAGE_VERSION` is now set by the calculation step.

## Risks / Trade-offs

| Risk | Impact | Mitigation |
|------|--------|------------|
| Python stdlib SemVer parsing misses edge case | Wrong version calculated | Comprehensive test suite covering all increment scenarios; SemVer is well-defined and narrow for our use case |
| `git tag` shows unexpected format | Script fails or produces wrong version | Strict regex matching; non-matching tags silently ignored |
| No tags on first release | Workflow blocked without override | Clear error message directing to use `version_override` for initial release |
| Mixed tag conventions (e.g., `rc`, `alpha`, `beta`) | Script behavior unclear | Non-`preview.N` prerelease tags are ignored by the preview detection; they still match as base SemVer for stable bumps |
| `uv run python ...` adds latency | ~2-3s per workflow run | Acceptable for a manual workflow; early error saves build minutes |
