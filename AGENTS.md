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
Bootstrap dependencies: `rtk brew bundle` (macOS) or `rtk make bundle`.

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
- CLI, Testing, and Unity depend **only** on Core.
- No circular dependencies between packages.
- `Core.Scanning` internals are protected — only Core itself may import them.

## Package layout
```
src/
  ArchLinterNet.Core/     — model, YAML loading, assembly resolution
  ArchLinterNet.Cli/      — .NET global/local tool CLI
  ArchLinterNet.Testing/  — test framework adapters
  ArchLinterNet.Unity/    — Unity .asmdef validation (optional)
tests/
  ArchLinterNet.Core.Tests/
  ArchLinterNet.Cli.Tests/
  ArchLinterNet.Unity.Tests/
```

## Conventions
- Private fields: `_camelCase`. Types/members: `PascalCase`. Interfaces: `IName`.
- No BDD/Gherkin — library project.
- File size thresholds: ≥500 lines warning, >800 lines error.

## OpenSpec workflow
- Specs live in `openspec/specs/<capability>/spec.md` — this is the source of truth.
- During proposal, new specs are created under the change directory.
- **Before archiving a change**, sync all new specs to `openspec/specs/<capability>/spec.md`.
  The archive workflow does NOT auto-promote new specs — only delta specs on existing ones.
- Active changes live in `openspec/changes/<name>/`. Archived changes live in
  `openspec/changes/archive/YYYY-MM-DD-<name>/`.

## State of the repo
- Early extraction / preview. Docs under `docs/` built with MkDocs. CI in `.github/workflows/`.
