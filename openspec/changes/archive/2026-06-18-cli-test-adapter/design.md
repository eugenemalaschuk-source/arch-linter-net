## Context

The CLI (`src/ArchLinterNet.Cli/Program.cs`) and test adapter (`src/ArchLinterNet.Testing/ArchitectureAssertions.cs`) were scaffolded during the extraction phase (#657) but left incomplete. The CLI supports `--policy`, `--mode`, and `--format` flags with correct exit codes, but has no integration tests, no .NET tool manifest, and no documentation. The test adapter provides `FromPolicy()`/`ShouldPass()`/`ValidateStrict()`/`ValidateAudit()` entry points but is not documented in the README. The `make audit-architecture` target is a stub.

The engine (`ArchLinterNet.Core`) is complete and stable. This change wraps it in a consumable, documented, testable tooling layer.

## Goals / Non-Goals

**Goals:**
- CLI has integration tests covering all argument combinations, exit codes, error cases, and output formats.
- CLI is installable as a .NET local tool via `dotnet tool restore`.
- `make audit-architecture` runs audit-mode validation via the CLI.
- README documents CLI usage, test adapter API, and CI wiring with examples.
- Sample repository shows a YAML policy and a test project using both CLI and test adapter.
- All existing behavior is preserved — no breaking changes to the CLI interface.

**Non-Goals:**
- Publishing 1.0.0 to NuGet (scoped to local tool + source consumption).
- Redesigning the CLI interface or adding subcommands (`validate`, `check`, etc.).
- Redesigning the YAML schema or contract model.
- Adding test framework support beyond NUnit.
- Adding a full CI pipeline for ArchLinterNet itself (separate concern).

## Decisions

| Decision | Choice | Alternative | Rationale |
|---|---|---|---|
| CLI distribution | .NET local tool (`PackAsTool` in csproj + `dotnet-tools.json`) | Source-only via `dotnet run` | Local tool is the standard .NET pattern; avoids forcing consumers to clone the repo. Issue #658 explicitly asks for a stable command shape. |
| CLI interface | Flat flags (`--policy`, `--mode`, `--format`) | Subcommand (`validate`) | Already implemented, stable, simpler. First Ice can adopt as-is. Subcommand pattern adds no value for a single-verb tool. |
| Test framework | NUnit | xUnit, MSTest | Repo-wide convention (AGENTS.md). Testing adapter targets NUnit's `Assert` model. |
| CLI test approach | Process-based integration tests (launch CLI binary) | Unit-test Program.cs via refactoring | Process-based tests validate the actual entry point, exit codes, and stdout/stderr without coupling test to internal structure. Existing Program.cs is top-level statements — refactoring for testability would be a larger change. |
| Audit Make target | `dotnet run --project src/ArchLinterNet.Cli -- --mode audit --policy architecture/dependencies.arch.yml` | Run via dedicated test project | CLI is the intended audit tool. Running it directly via `dotnet run` avoids adding a separate test project for a single Make target. |

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| **Process-based CLI tests are fragile** if the binary path/build changes. | Pin the test to `dotnet run --project` using the known project path. Tests run in CI the same way devs run locally. |
| **.NET local tool manifest pins a specific package source** — if NuGet source changes, `dotnet tool restore` fails. | Document that the tool is source-built; the manifest points to the local build output. For NuGet-published builds (future), the manifest would change to the public feed. |
| **CLI args parsing is manual** (no System.CommandLine) — edge cases in flag handling. | Current manual parsing is simple and stable. Integration tests cover all flag combinations and error cases. Move to System.CommandLine only if the CLI grows subcommands. |
| **`make audit-architecture` depends on `dotnet build` being up-to-date.** | `dotnet run` handles this implicitly. No additional dependency tracking needed. |

## Open Questions

- Should `make audit-architecture` accept a `--format json` flag for CI artifact capture, or keep it hardcoded to human-readable for now?
- Does the sample policy need to be a full standalone project, or a reference snippet in the README?
