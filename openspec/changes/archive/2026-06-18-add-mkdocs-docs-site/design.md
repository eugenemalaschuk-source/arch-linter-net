## Context

ArchLinterNet currently has:
- `docs/README.md` — internal API reference (258 lines), useful for contributors
- `README.md` — project root README, focused on contributors
- No user-facing documentation site

The project follows the First Ice tooling pattern: Python dependencies in `tools/pyproject.toml`, managed via `uv`, committed lock file, and `.venv` at repo root. uv is already installed via `Brewfile` and available in the environment.

First Ice's Python setup:
```
tools/pyproject.toml   — [project] name, version, requires-python, dependencies
tools/uv.lock          — committed after resolution
.venv/                 — at repository root
Makefile               — `make venv` target via uv sync --project tools/pyproject.toml
```

## Goals / Non-Goals

**Goals:**
- Create a working MkDocs documentation site for ArchLinterNet users
- Follow the First Ice Python tooling convention exactly
- Cover installation, CLI usage, policy authoring, CI integration, migration baselines, contract families, and AI section
- Enable local preview via `make docs-serve`
- Enable CI build via `make docs-build`

**Non-Goals:**
- GitHub Pages deployment (separate task)
- Full AI policy authoring content (issue #662)
- Replacing `docs/README.md` (it stays as project/contributor docs)
- Replacing the root `README.md` (it stays as project overview)
- Custom website outside MkDocs
- Final exhaustive reference for every future feature

## Decisions

### 1. Python tooling: uv + tools/pyproject.toml (matching First Ice exactly)
- **Rationale**: The project already has `uv` installed via Brewfile. Using `tools/pyproject.toml` instead of root-level `pyproject.toml` avoids conflict with potential future Python packaging needs. First Ice uses the same pattern.
- **Alternatives considered**: Root-level `pyproject.toml` (rejected — would conflate docs tooling with potential project packaging).

### 2. Theme: mkdocs-material
- **Rationale**: Most popular MkDocs theme with built-in search, navigation, code highlighting, dark mode, and responsive design. No additional config needed beyond a simple `theme: name: material`.
- **Alternatives considered**: Plain mkdocs (rejected — requires manual CSS/JS for basic features), ReadTheDocs theme (viable but less feature-rich).

### 3. Documentation structure: flat + grouped by topic
- **Rationale**: The nav structure groups related pages under Guides, Contracts, and Reference sections. This keeps the nav shallow enough for quick scanning while organizing content logically.
- **Alternatives considered**: Deeply nested hierarchy (rejected — creates navigation burden for common tasks).

### 4. `.venv` at repository root
- **Rationale**: `uv sync` with `--project tools/pyproject.toml` creates the virtual environment at the project root. Make targets expect `.venv` at root. Follows First Ice pattern exactly.
- **Mitigation**: `.venv/` added to `.gitignore` to prevent accidental commits.

### 5. Docs pages content from existing docs/README.md
- **Rationale**: `docs/README.md` already contains detailed API reference content (contract types, execution model, YAML format, code examples). Rather than writing from scratch, relevant sections will be adapted into the new user-facing pages.
- **Approach**: Content is *migrated and adapted*, not copied verbatim — the audience is different (users vs contributors).

## Risks / Trade-offs

- **Content duplication**: Some information lives in both `docs/README.md` (contributor reference) and new MkDocs pages (user docs). The two audiences have different needs, so some overlap is intentional. The `docs/README.md` focuses on internal API surface and contributor workflow; MkDocs focuses on user workflows.
- **Python tooling drift**: If MkDocs or its dependencies diverge from what Python is available, docs build could break. Mitigation: committed `uv.lock` pins exact versions.
- **No automated deploy**: Without GitHub Pages deployment (separate task), the docs only exist locally or in CI artifacts. Users won't have a published URL yet — acceptable per scope.
