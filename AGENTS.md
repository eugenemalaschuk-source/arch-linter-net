# Agent Instructions

## Project
- **ArchLinterNet** — declarative architecture contracts and dependency linting for .NET.
- Stack: .NET 10 + C#. Tests use **NUnit**.
- Solution file: `ArchLinterNet.slnx` (`.slnx` format, not `.sln`).
- `TreatWarningsAsErrors` enabled globally in `Directory.Build.props`.

## RTK — mandatory CLI prefix
Every shell command MUST be prefixed with `rtk`:
```
rtk make verify
rtk dotnet restore
```
Bootstrap dependencies: `rtk brew bundle` (macOS) or `rtk make bundle`.

## Key commands
```
rtk make restore              # NuGet restore (required before any --no-restore target)
rtk make fmt                  # dotnet format — auto-format all C# code
rtk make lint                 # lint-code-size + lint-dotnet-format + lint-architecture
rtk make lint-architecture    # strict self-architecture validation (via Core.Tests)
rtk make lint-code-size       # file size lint (warn ≥500, error ≥800 lines)
rtk make test                 # run all tests
rtk make verify               # lint + all tests
```
All `dotnet test`/`dotnet format` targets use `--no-restore` — run `restore` first when adding/changing dependencies.

Run a single test project:
```
rtk dotnet test tests/ArchLinterNet.Core.Tests --no-restore
```

## Architecture governance
File: `architecture/dependencies.arch.yml`. Enforced via `lint-architecture`.

Direction rules:
- CLI, Testing, and Unity depend **only** on Core.
- No circular dependencies between packages.
- `audit-architecture` is a stub (not yet implemented; falls back to strict).

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

## State of the repo
- Early extraction / preview. `docs/` is empty. No `.github/` CI config exists yet.
