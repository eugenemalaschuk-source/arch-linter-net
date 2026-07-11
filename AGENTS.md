# Agent Instructions

## Project
- **ArchLinterNet** — declarative architecture contracts and dependency linting for .NET.
- Stack: .NET 10 + C#. Tests use **NUnit**.
- Solution file: `ArchLinterNet.slnx` (`.slnx` format, not `.sln`).
- `TreatWarningsAsErrors` enabled globally in `Directory.Build.props`.

## RTK — mandatory CLI prefix
Every shell command MUST be prefixed with `rtk`:
```
rtk make acceptance
rtk dotnet restore
```

RTK itself is bootstrapped the same way as the rest of the developer toolchain:
- `Brewfile` includes `brew "rtk"` for macOS/Linux Homebrew installs.
- `make bundle` runs the platform-specific `tools/scripts/configure_rtk_*` script.
- `make rtk-init` re-runs only the RTK agent integration setup.

If `rtk` is missing and therefore cannot prefix commands yet, run only the RTK bootstrap script directly, then restart the shell/session and return to the mandatory `rtk` prefix:
```
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/scripts/configure_rtk_windows.ps1
sh tools/scripts/configure_rtk_unix.sh
```

Bootstrap dependencies after RTK is available: `rtk brew bundle` (macOS) or `rtk make bundle`.
Pinned bootstrap versions and their upgrade procedure are documented in `docs/internal/dependency-maintenance.md`.

## Key commands
```
rtk make setup                # full bootstrap: bundle + restore + venv (run once)
rtk make restore              # NuGet restore (required before any --no-restore target)
rtk make fmt                  # dotnet format — auto-format all C# code
rtk make lint                 # lint-code-size + lint-dotnet-format + lint-architecture
rtk make lint-architecture    # strict self-architecture validation (via Core.Tests)
rtk make lint-code-size       # file size lint (warn ≥500, error ≥800 lines)
rtk make test                 # run all tests
rtk make acceptance           # lint + all tests
rtk make architecture-coverage-report  # full-solution coverage report (Markdown + JSON) on demand
```
All `dotnet test`/`dotnet format` targets use `--no-restore` — run `restore` first when adding/changing dependencies.

Run a single test project:
```
rtk dotnet test tests/ArchLinterNet.Core.Tests --no-restore
```

Run the CLI directly:
```
rtk dotnet run --project src/ArchLinterNet.Cli -- --policy architecture/dependencies.arch.yml --mode strict
```

## Docs workflow
```
rtk make venv                 # create Python virtual environment (one-time)
rtk make docs-serve           # start local preview at http://127.0.0.1:8000
rtk make docs-build           # build static site to site/
rtk make fmt-docs             # auto-format markdown documentation
rtk make lint-docs            # verify MkDocs documentation structure
```

## Backlog governance
File: `docs/ai/backlog-governance.md`.

Before creating or updating GitHub issues, agents MUST apply the backlog governance rules:
- use typed titles such as `[STORY][AI] Tooling: ...` and `[TASK][AI] Tooling: ...`;
- use the controlled title verbs from the governance document;
- link issues explicitly with `Parent story: #...`, `Depends on: #...`, and `Related: #...` where applicable;
- include the required sections: `Goal`, `Work type`, `Context`, `What to do`, `Manual tasks`, `AI-friendly tasks`, `Estimate`, `Acceptance criteria`, `Validation`, and `Non-goals`;
- estimate the developer's real hands-on time with AI assistance;
- keep architecture-governance and release-pipeline task rules aligned with the governance document.

Do not create isolated implementation tasks without an existing story or a newly proposed story.

## Architecture governance
File: `architecture/dependencies.arch.yml`. Enforced via `lint-architecture`.

Direction rules:
- CLI and Testing depend **only** on Core.
- Unity `.asmdef` validation is a Core capability, not a separate adapter assembly.
- No circular dependencies between packages.
- `Core.Scanning` internals are protected — only Core itself may import them.

## Package layout
```
src/
  ArchLinterNet.Core/     — model, YAML loading, assembly resolution, asmdef validation
  ArchLinterNet.Cli/      — .NET global/local tool CLI
  ArchLinterNet.Testing/  — test framework adapters
tests/
  ArchLinterNet.Core.Tests/
  ArchLinterNet.Cli.Tests/
```

## Conventions
- Private fields: `_camelCase`. Types/members: `PascalCase`. Interfaces: `IName`.
- No BDD/Gherkin — library project.
- File size thresholds: ≥500 lines warning, >800 lines error.

## OpenSpec workflow
- Specs live in `openspec/specs/<capability>/spec.md` — this is the source of truth.
  Each spec file MUST have a `## Purpose` section and a `## Requirements` section;
  it must NOT contain a delta header (`## ADDED/MODIFIED/REMOVED/RENAMED Requirements`).
- During proposal, new capabilities are written as delta specs (`## ADDED Requirements`)
  under the change directory.
- **To finish a change**, run `openspec archive <change-name>`. It rebuilds
  `openspec/specs/<capability>/spec.md` from the change's delta specs for both new
  and existing capabilities — do not copy a delta spec file into `openspec/specs/`
  directly, since that leaves an invalid delta header in the main spec.
- Active changes live in `openspec/changes/<name>/`. Archived changes live in
  `openspec/changes/archive/YYYY-MM-DD-<name>/`.
- Run `openspec validate --all` after archiving or any manual spec edit.

## State of the repo
- Early extraction / preview. Docs under `docs/` built with MkDocs. CI in `.github/workflows/`.
