## Why

Issue #658 (parent story #655) requires a documented, stable CLI and test adapter for ArchLinterNet so that First Ice and other repositories can run strict and audit architecture validation from YAML policy files — without embedding the reusable engine in every project. The CLI and testing adapter exist as scaffolds but lack tests, documentation, sample usage, and a local tool distribution path.

## What Changes

- Add CLI integration tests for argument parsing, strict/audit modes, human and JSON output formats, error handling (invalid args, missing file, runtime errors), and exit codes.
- Add a .NET local tool manifest (`dotnet-tools.json`) so the CLI can run via `dotnet arch-linter-net` after a local tool restore. Add a NuGet-specific tool manifest configuration for the CLI project.
- Add a `Makefile` sample and CI wiring example showing how to wire strict validation as a blocking gate and audit as diagnostics.
- Document the CLI and test adapter API in the project README.
- Wire the `make audit-architecture` target to use `--mode audit` instead of being a stub.
- Add a sample repository with a YAML policy and a test project that demonstrates both strict and audit usage.
- Replace the placeholder CLI unit test with real integration tests.

## Capabilities

### New Capabilities
- `cli-validation`: Command-line interface for running strict and audit architecture validation. Supports `--policy`, `--mode`, and `--format` flags. Returns exit codes 0/1/2. Supports human-readable terminal output and JSON machine-readable output.
- `test-adapter`: NUnit-compatible test API (`ArchitectureAssertions.FromPolicy()`, `ShouldPass()`, `ValidateStrict()`, `ValidateAudit()`) for embedding architecture validation in a test suite.
- `sample-policy`: A documented sample repository with a YAML policy file and a test project showing strict/audit usage with NUnit.

### Modified Capabilities

None — no existing specs to modify.

## Impact

- `src/ArchLinterNet.Cli/`: Requires a proper .NET tool manifest (`ToolCommandName`) in the csproj.
- `src/ArchLinterNet.Cli/Program.cs`: No behavior changes anticipated. If tests reveal bugs, minor fixes may be needed.
- `make/lint.mk`: The `audit-architecture` target will be changed from a stub to an actual audit invocation.
- `README.md`: New documentation section covering CLI usage, test adapter API, and CI wiring.
- New files: `samples/` will be expanded with sample test usage.
